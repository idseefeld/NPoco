using System;
using Microsoft.Extensions.Configuration;

namespace NPoco.Tests.Common
{
    public static class TestConfig
    {
        // config.json, overridable by NPOCO_-prefixed environment variables,
        // e.g. NPOCO_TestDBType=7 dotnet test
        public static IConfiguration Configuration { get; } = new ConfigurationBuilder()
            .AddJsonFile("config.json")
            .AddEnvironmentVariables("NPOCO_")
            .Build();

        public static int TestDBType => Convert.ToInt32(Configuration["TestDBType"]);

        public static string PostgresConnectionString =>
            string.IsNullOrEmpty(Configuration["TestDbPostgresConnectionString"])
                ? "Host=localhost;Username=postgres;Database=postgres"
                : Configuration["TestDbPostgresConnectionString"];
    }
}
