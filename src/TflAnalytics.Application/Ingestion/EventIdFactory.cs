using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace TflAnalytics.Application.Ingestion;

public static class EventIdFactory
{
    public static string Create(
        string eventType,
        DateTimeOffset observedAtUtc,
        TimeSpan observationWindow,
        params string?[] identityParts)
    {
        var window = observedAtUtc.UtcTicks / observationWindow.Ticks;
        var value = string.Join(
            "|",
            new[]
            {
                eventType,
                window.ToString(CultureInfo.InvariantCulture)
            }.Concat(identityParts.Select(part => part?.Trim() ?? string.Empty)));

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }
}
