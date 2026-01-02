using System;
using System.Collections.Generic;
using System.Linq;

using ClinicBooking.Api.Api.Options;
using ClinicBooking.Api.Infrastructure.Data;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ClinicBooking.Tests;

public sealed class TestApiFactory : WebApplicationFactory<Program>
{
    public const string ValidKey1 = "test-key-1";
    public const string ValidKey2 = "test-key-2";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((ctx, config) =>
        {
            // ✅ Injecte des clés valides pour que PreAuth reconnaisse "ValidKey1/2"
            var s = ApiKeyOptions.SectionName;

            var dict = new Dictionary<string, string?>
            {
                [$"{s}:HeaderName"] = "X-API-KEY",
                [$"{s}:Keys:0"] = ValidKey1,
                [$"{s}:Keys:1"] = ValidKey2
            };

            config.AddInMemoryCollection(dict);
        });

        builder.ConfigureServices(services =>
        {
            // ✅ Remplace Postgres par InMemory pour ne pas dépendre du DB local
            var dbDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ClinicDbContext>));
            if (dbDescriptor is not null)
                services.Remove(dbDescriptor);

            services.AddDbContext<ClinicDbContext>(opt =>
                opt.UseInMemoryDatabase($"clinicbooking-tests-{Guid.NewGuid():N}")
            );

            // ✅ Build + EnsureCreated
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
            db.Database.EnsureCreated();
        });
    }
}
