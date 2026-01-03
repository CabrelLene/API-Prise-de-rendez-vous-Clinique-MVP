using System;
using System.Collections.Generic;
using System.Linq;

using ClinicBooking.Api.Infrastructure.Data;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ClinicBooking.Api.Tests;

public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            var dict = new Dictionary<string, string?>
            {
                ["ApiKey:HeaderName"] = "X-API-KEY",
                ["ApiKey:Keys:0"] = "test-key",
                ["ConnectionStrings:Default"] = "Host=unused;Database=unused;Username=unused;Password=unused"
            };

            config.AddInMemoryCollection(dict);
        });

        builder.ConfigureServices(services =>
        {
            // Retire le DbContext Npgsql existant
            var dbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ClinicDbContext>)
            );

            if (dbContextDescriptor != null)
                services.Remove(dbContextDescriptor);

            // Remplace par InMemory
            services.AddDbContext<ClinicDbContext>(opt =>
                opt.UseInMemoryDatabase("ClinicBooking_TestDb"));
        });
    }
}
