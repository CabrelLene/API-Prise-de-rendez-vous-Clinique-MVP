using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

using Xunit;

namespace ClinicBooking.Api.Tests;

public class SmokeTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public SmokeTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Root_Is_Public_And_Returns_200()
    {
        var res = await _client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var json = await res.Content.ReadAsStringAsync();
        Assert.Contains("ClinicBooking API", json);
    }

    [Fact]
    public async Task Health_Is_Public_And_Returns_200()
    {
        var res = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Appointments_Without_Key_Returns_401()
    {
        var res = await _client.GetAsync("/appointments?page=1&pageSize=1");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);

        var body = await res.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("API_KEY_MISSING", doc.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Appointments_With_Key_Returns_200()
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "/appointments?page=1&pageSize=1");
        req.Headers.Add("X-API-KEY", "test-key");

        var res = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }
}
