namespace ClinicBooking.Api.Api.Options;

public sealed class ApiKeyOptions
{
    public const string SectionName = "ApiKey";

    public string HeaderName { get; set; } = "X-API-KEY";
    public string Key { get; set; } = "";
}
