using System.Collections.Generic;
using ClinicBooking.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ClinicBooking.Tests;

public sealed class TestApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApiKey:HeaderName"] = "X-API-KEY",
                ["ApiKey:Keys:0"] = "dev-secret-123",
                ["ApiKey:Keys:1"] = "dev-secret-456",

                // juste pour éviter un null si le code lit la config
                ["ConnectionStrings:Default"] = "Host=ignored;Database=ignored;Username=ignored;Password=ignored"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remplace Npgsql par InMemory
            services.RemoveAll(typeof(DbContextOptions<ClinicDbContext>));
            services.AddDbContext<ClinicDbContext>(opt =>
                opt.UseInMemoryDatabase("clinicbooking-tests"));
        });
    }
}
