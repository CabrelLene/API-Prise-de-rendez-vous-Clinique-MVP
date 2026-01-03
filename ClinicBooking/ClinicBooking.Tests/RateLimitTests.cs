using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace ClinicBooking.Tests;

public class RateLimitTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;

    public RateLimitTests(TestApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Appointments_WithValidKey_Should429_After10()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // 10 OK
        for (int i = 0; i < 10; i++)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "/appointments?page=1&pageSize=1");
            req.Headers.Add("X-API-KEY", TestApiFactory.ValidKey1);

            var res = await client.SendAsync(req);
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        }

        // 11e => 429
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "/appointments?page=1&pageSize=1");
            req.Headers.Add("X-API-KEY", TestApiFactory.ValidKey1);

            var res = await client.SendAsync(req);
            Assert.Equal(HttpStatusCode.TooManyRequests, res.StatusCode);
        }
    }

    [Fact]
    public async Task InvalidKey_ShouldEventually429_PreAuthIpLimiter()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        async Task<HttpResponseMessage> SendInvalid()
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "/appointments?page=1&pageSize=1");
            req.Headers.Add("X-API-KEY", "invalid");
            return await client.SendAsync(req);
        }

        // 1) Peut être 403 (normal) OU 429 (si quota IP déjà consommé par d'autres tests).
        var first = await SendInvalid();
        Assert.True(
            first.StatusCode == HttpStatusCode.Forbidden ||
            first.StatusCode == HttpStatusCode.TooManyRequests,
            $"Expected 403 or 429, got {(int)first.StatusCode} {first.StatusCode}"
        );

        // 2) On insiste jusqu'à obtenir 429 => preuve que le PreAuth limiter fonctionne.
        var last = first.StatusCode;

        for (int i = 0; i < 200; i++)
        {
            var res = await SendInvalid();
            last = res.StatusCode;

            if (last == HttpStatusCode.TooManyRequests)
                break;
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, last);
    }
}
