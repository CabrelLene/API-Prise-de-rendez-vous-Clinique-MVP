using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace ClinicBooking.Tests;

public sealed class RateLimitTests : IClassFixture<TestApiFactory>
{
    private readonly HttpClient _client;

    public RateLimitTests(TestApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Appointments_WithValidKey_Should429_After10()
    {
        // 10 requêtes -> pas 429
        for (var i = 1; i <= 10; i++)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "/appointments?page=1&pageSize=1");
            req.Headers.Add("X-API-KEY", "dev-secret-123");

            var res = await _client.SendAsync(req);
            Assert.NotEqual((HttpStatusCode)429, res.StatusCode);
        }

        // 11e -> 429
        using var req11 = new HttpRequestMessage(HttpMethod.Get, "/appointments?page=1&pageSize=1");
        req11.Headers.Add("X-API-KEY", "dev-secret-123");

        var res11 = await _client.SendAsync(req11);
        Assert.Equal((HttpStatusCode)429, res11.StatusCode);
    }

    [Fact]
    public async Task InvalidKey_ShouldEventually429_PreAuthIpLimiter()
    {
        var got403 = false;
        var got429 = false;

        for (var i = 1; i <= 30; i++)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "/appointments?page=1&pageSize=1");
            req.Headers.Add("X-API-KEY", $"invalid-{i}");

            var res = await _client.SendAsync(req);

            if (res.StatusCode == HttpStatusCode.Forbidden) got403 = true;
            if ((int)res.StatusCode == 429) { got429 = true; break; }
        }

        Assert.True(got403);
        Assert.True(got429);
    }
}
