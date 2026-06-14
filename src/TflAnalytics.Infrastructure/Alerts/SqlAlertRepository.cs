using Azure.Core;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using TflAnalytics.Application.Alerts;
using TflAnalytics.Contracts.Alerts;

namespace TflAnalytics.Infrastructure.Alerts;

public sealed class SqlAlertRepository : IAlertRepository
{
    private static readonly string[] SqlScopes =
        ["https://database.windows.net/.default"];

    private readonly AlertStorageOptions _options;
    private readonly TokenCredential _credential;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _initialized;

    public SqlAlertRepository(
        IOptions<AlertStorageOptions> options,
        TokenCredential credential)
    {
        _options = options.Value;
        _credential = credential;
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
