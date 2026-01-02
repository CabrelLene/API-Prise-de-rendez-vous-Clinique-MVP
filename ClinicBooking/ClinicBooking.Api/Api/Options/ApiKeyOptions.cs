namespace ClinicBooking.Api.Api.Options;

public sealed class ApiKeyOptions
{
    public const string SectionName = "ApiKey";

    public string HeaderName { get; set; } = "X-API-KEY";

    // ✅ nouveau: plusieurs clés
    public string[] Keys { get; set; } = Array.Empty<string>();

    // ✅ compat (si tu avais encore "Key" dans un env)
    public string? Key { get; set; }
}
