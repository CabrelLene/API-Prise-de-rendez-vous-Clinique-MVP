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

        // Selon ton PreAuth (souvent 5/min), tu as 403 puis 429.
        // Ici on check: les 5 premières => 403, la 6e => 429
        for (int i = 0; i < 5; i++)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "/appointments?page=1&pageSize=1");
            req.Headers.Add("X-API-KEY", "invalid");

            var res = await client.SendAsync(req);
            Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        }

        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "/appointments?page=1&pageSize=1");
            req.Headers.Add("X-API-KEY", "invalid");

            var res = await client.SendAsync(req);
            Assert.Equal(HttpStatusCode.TooManyRequests, res.StatusCode);
        }
    }
}
