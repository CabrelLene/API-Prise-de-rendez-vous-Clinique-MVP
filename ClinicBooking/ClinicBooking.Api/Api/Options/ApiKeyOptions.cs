namespace ClinicBooking.Api.Api.Options;

public sealed class ApiKeyOptions
{
    public const string SectionName = "ApiKey";

    public string HeaderName { get; set; } = "X-API-KEY";

    // ✅ Plusieurs clés autorisées
    public string[] Keys { get; set; } = Array.Empty<string>();
}
