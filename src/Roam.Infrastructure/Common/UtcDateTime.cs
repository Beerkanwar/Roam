namespace Roam.Infrastructure.Common;

/// <summary>
/// Normalizes client-supplied <see cref="DateTime"/> values to UTC.
/// System.Text.Json produces <see cref="DateTimeKind.Local"/> for timestamps carrying a non-UTC offset and
/// <see cref="DateTimeKind.Unspecified"/> for timestamps without one; Npgsql rejects non-UTC values for
/// "timestamp with time zone" columns, and blindly calling SpecifyKind on a Local value shifts the instant.
/// </summary>
internal static class UtcDateTime
{
    public static DateTime Normalize(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
