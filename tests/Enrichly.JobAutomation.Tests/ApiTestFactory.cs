using Enrichly.JobAutomation.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Enrichly.JobAutomation.Tests;

/// <summary>
/// Boots the real API pipeline (controllers, JWT auth, DI, middleware) against a disposable
/// SQL Server database, so the authorization and idempotency tests exercise the actual HTTP
/// behavior rather than a mock.
///
/// These are integration tests, not unit tests: they require a reachable SQL Server.
/// By default they target the local SQL Server Express instance (MARK2\SQLEXPRESS05) with
/// a uniquely named throwaway database per test run.
/// Override with the TEST_SQL_CONNECTION environment variable to point elsewhere (Docker,
/// Azure SQL, a different local instance). If no SQL Server is reachable, these tests fail
/// with a connection error rather than silently passing - that is intentional, since a
/// skipped test would hide the exact thing this project needs proven (see ENGINEERING.md
/// "Known Limitations").
/// </summary>
public sealed class ApiTestFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public string ConnectionString { get; } = CreateTestConnectionString();

    private static string CreateTestConnectionString()
    {
        // Never run integration tests against a shared/existing database. If TEST_SQL_CONNECTION
        // is supplied, reuse its server/authentication settings but always replace the database
        // name with a unique throwaway database. This prevents stale schemas (for example, a DB
        // created before hourly scheduling added IsHourly/NextScheduledAt) from breaking tests.
        var configured = Environment.GetEnvironmentVariable("TEST_SQL_CONNECTION");
        var connectionString = string.IsNullOrWhiteSpace(configured)
            ? "Server=MARK2\\SQLEXPRESS05;Trusted_Connection=True;TrustServerCertificate=True"
            : configured;

        var builder = new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = $"EnrichlyJobAutomationTests_{Guid.NewGuid():N}"
        };

        return builder.ConnectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SqlServer"] = ConnectionString
            });
        });
    }

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<JobAutomationDbContext>();
        await db.Database.MigrateAsync();
    }

    // Hides (not overrides) WebApplicationFactory's ValueTask DisposeAsync so xUnit's
    // IAsyncLifetime.DisposeAsync (Task-returning) resolves to this instead.
    // Each test run uses a uniquely named throwaway database on the configured SQL Server
    // instance; drop old "EnrichlyJobAutomationTests_*" databases periodically if disk space matters.
    public new Task DisposeAsync() => Task.CompletedTask;
}
