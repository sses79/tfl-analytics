using Azure.Core;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TflAnalytics.Application.Alerts;
using TflAnalytics.Contracts.Alerts;
using TflAnalytics.Contracts.Dashboard;

namespace TflAnalytics.Infrastructure.Alerts;

public sealed class SqlAlertRepository : IAlertRepository
{
    private static readonly string[] SqlScopes =
        ["https://database.windows.net/.default"];

    private readonly AlertStorageOptions _options;
    private readonly TokenCredential _credential;
    private readonly ILogger<SqlAlertRepository> _logger;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _initialized;

    public SqlAlertRepository(
        IOptions<AlertStorageOptions> options,
        TokenCredential credential,
        ILogger<SqlAlertRepository> logger)
    {
        _options = options.Value;
        _credential = credential;
        _logger = logger;
    }

    public async Task EnsureInitializedAsync(
        CancellationToken cancellationToken = default)
    {
        if (_initialized || !_options.Initialize)
        {
            return;
        }

        await _initializationLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return;
            }

            await EnsureDatabaseAsync(cancellationToken);
            await using var connection = await OpenConnectionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                IF OBJECT_ID(N'dbo.Alerts', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.Alerts
                    (
                        AlertId nvarchar(64) NOT NULL
                            CONSTRAINT PK_Alerts PRIMARY KEY,
                        RuleType nvarchar(64) NOT NULL,
                        SourceEventId nvarchar(128) NOT NULL,
                        DetectedAtUtc datetimeoffset NOT NULL,
                        ObservedAtUtc datetimeoffset NOT NULL,
                        StationId nvarchar(64) NULL,
                        LineId nvarchar(64) NULL,
                        VehicleId nvarchar(64) NULL,
                        Title nvarchar(200) NOT NULL,
                        Description nvarchar(1000) NOT NULL,
                        PreviousValue nvarchar(256) NOT NULL,
                        CurrentValue nvarchar(256) NOT NULL
                    );

                    CREATE INDEX IX_Alerts_DetectedAtUtc
                        ON dbo.Alerts (DetectedAtUtc DESC);
                END;
            """;
            await command.ExecuteNonQueryAsync(cancellationToken);

            _initialized = true;

            if (!string.IsNullOrWhiteSpace(_options.ApiIdentityName))
            {
                try
                {
                    await GrantApiIdentityAccessAsync(
                        connection,
                        _options.ApiIdentityName,
                        _options.ApiObjectId,
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Could not grant SQL access to API identity '{Identity}'; API read endpoints may fail.",
                        _options.ApiIdentityName);
                }
            }
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    public async Task<bool> CreateAsync(
        AlertCandidate alert,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO dbo.Alerts
            (
                AlertId,
                RuleType,
                SourceEventId,
                DetectedAtUtc,
                ObservedAtUtc,
                StationId,
                LineId,
                VehicleId,
                Title,
                Description,
                PreviousValue,
                CurrentValue
            )
            VALUES
            (
                @alertId,
                @ruleType,
                @sourceEventId,
                @detectedAtUtc,
                @observedAtUtc,
                @stationId,
                @lineId,
                @vehicleId,
                @title,
                @description,
                @previousValue,
                @currentValue
            );
            """;
        command.Parameters.AddWithValue("@alertId", alert.AlertId);
        command.Parameters.AddWithValue("@ruleType", alert.RuleType);
        command.Parameters.AddWithValue("@sourceEventId", alert.SourceEventId);
        command.Parameters.AddWithValue("@detectedAtUtc", alert.DetectedAtUtc);
        command.Parameters.AddWithValue("@observedAtUtc", alert.ObservedAtUtc);
        command.Parameters.AddWithValue(
            "@stationId",
            (object?)alert.StationId ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "@lineId",
            (object?)alert.LineId ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "@vehicleId",
            (object?)alert.VehicleId ?? DBNull.Value);
        command.Parameters.AddWithValue("@title", alert.Title);
        command.Parameters.AddWithValue("@description", alert.Description);
        command.Parameters.AddWithValue("@previousValue", alert.PreviousValue);
        command.Parameters.AddWithValue("@currentValue", alert.CurrentValue);

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
            return true;
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<AlertSummary>> GetRecentAlertsAsync(
        int maxCount = 50,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT TOP {maxCount}
                AlertId, RuleType, SourceEventId, DetectedAtUtc, ObservedAtUtc,
                StationId, LineId, VehicleId, Title, Description, PreviousValue, CurrentValue
            FROM dbo.Alerts
            ORDER BY DetectedAtUtc DESC
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var results = new List<AlertSummary>();
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new AlertSummary(
                reader.GetString(reader.GetOrdinal("AlertId")),
                reader.GetString(reader.GetOrdinal("RuleType")),
                reader.IsDBNull(reader.GetOrdinal("StationId"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("StationId")),
                reader.IsDBNull(reader.GetOrdinal("LineId"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("LineId")),
                reader.GetString(reader.GetOrdinal("Title")),
                reader.GetString(reader.GetOrdinal("Description")),
                reader.GetString(reader.GetOrdinal("PreviousValue")),
                reader.GetString(reader.GetOrdinal("CurrentValue")),
                reader.GetDateTimeOffset(reader.GetOrdinal("DetectedAtUtc")),
                reader.GetDateTimeOffset(reader.GetOrdinal("ObservedAtUtc"))));
        }

        return results;
    }

    private static async Task GrantApiIdentityAccessAsync(
        SqlConnection connection,
        string identityName,
        string? objectId,
        CancellationToken cancellationToken)
    {
        var quoted = $"[{identityName.Replace("]", "]]", StringComparison.Ordinal)}]";
        var escaped = EscapeSqlLiteral(identityName);
        var createUser = string.IsNullOrWhiteSpace(objectId)
            ? $"CREATE USER {quoted} FROM EXTERNAL PROVIDER"
            : $"CREATE USER {quoted} FROM EXTERNAL PROVIDER WITH OBJECT_ID='{EscapeSqlLiteral(objectId)}'";

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'{escaped}')
                EXEC('{EscapeSqlLiteral(createUser)}')
            IF IS_ROLEMEMBER('db_datareader', N'{escaped}') = 0
                EXEC('ALTER ROLE db_datareader ADD MEMBER {EscapeSqlLiteral(quoted)}')
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<SqlConnection> OpenConnectionAsync(
        CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(CreateConnectionString());
        if (string.IsNullOrWhiteSpace(_options.ConnectionString))
        {
            var token = await _credential.GetTokenAsync(
                new TokenRequestContext(SqlScopes),
                cancellationToken);
            connection.AccessToken = token.Token;
        }

        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private async Task EnsureDatabaseAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ConnectionString))
        {
            return;
        }

        var builder = new SqlConnectionStringBuilder(_options.ConnectionString)
        {
            InitialCatalog = "master"
        };
        await using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var databaseName = QuoteIdentifier(_options.DatabaseName);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            IF DB_ID(N'{EscapeSqlLiteral(_options.DatabaseName)}') IS NULL
                CREATE DATABASE {databaseName};
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private string CreateConnectionString()
    {
        if (!string.IsNullOrWhiteSpace(_options.ConnectionString))
        {
            return new SqlConnectionStringBuilder(_options.ConnectionString)
            {
                InitialCatalog = _options.DatabaseName
            }.ConnectionString;
        }

        if (string.IsNullOrWhiteSpace(_options.ServerFqdn))
        {
            throw new InvalidOperationException(
                "Configure AlertStorage:ConnectionString locally or "
                + "AlertStorage:ServerFqdn in Azure.");
        }

        return new SqlConnectionStringBuilder
        {
            DataSource = $"tcp:{_options.ServerFqdn},1433",
            InitialCatalog = _options.DatabaseName,
            Encrypt = true,
            TrustServerCertificate = false,
            ConnectTimeout = 30
        }.ConnectionString;
    }

    private static string QuoteIdentifier(string value) =>
        $"[{value.Replace("]", "]]", StringComparison.Ordinal)}]";

    private static string EscapeSqlLiteral(string value) =>
        value.Replace("'", "''", StringComparison.Ordinal);
}
