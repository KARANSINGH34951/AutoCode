using System.Collections.Concurrent;
using Enrichly.JobAutomation.Domain.Entities;
using Enrichly.JobAutomation.Domain.Enums;
using Enrichly.JobAutomation.Infrastructure.Persistence;
using Enrichly.JobAutomation.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Enrichly.JobAutomation.Tests;

/// <summary>
/// This is the single most important test in the project: it proves the assignment's core
/// requirement - "a job execution should not accidentally run multiple times just because
/// two workers picked it up at the same time" - against a real SQL Server database, not a
/// code review of the claim query. Prior validation only ran one Worker process at a time.
/// </summary>
public sealed class ExecutionClaimConcurrencyTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    private JobAutomationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<JobAutomationDbContext>().UseSqlServer(factory.ConnectionString).Options);

    [Fact]
    public async Task ConcurrentWorkers_NeverClaimTheSameExecutionTwice()
    {
        const int executionCount = 12;
        const int simulatedWorkerCount = 4;

        Guid jobId;
        await using (var setupDb = CreateContext())
        {
            var owner = new User { Email = $"claim-test-{Guid.NewGuid():N}@test.local", PasswordHash = "not-a-real-hash" };
            var job = new Job
            {
                Owner = owner,
                Name = "Concurrency test job",
                TargetUrl = "https://httpbin.org/post",
                IsEnabled = true,
                IsHourly = false,
                NextScheduledAt = null
            };
            setupDb.Users.Add(owner);
            setupDb.Jobs.Add(job);
            for (var i = 0; i < executionCount; i++)
            {
                setupDb.JobExecutions.Add(new JobExecution { JobId = job.Id });
            }
            await setupDb.SaveChangesAsync();
            jobId = job.Id;
        }

        // Simulate several Worker processes hammering the claim query at the same time,
        // each racing to grab whatever is next. This test intentionally creates executions
        // directly because it verifies the Worker claim layer, not the hourly scheduler.
        // Scheduled jobs eventually enter this exact same Pending -> claimed-by-one-worker
        // path, so adding hourly scheduling must not weaken the concurrency guarantee.
        var claimedExecutionIds = new ConcurrentBag<Guid>();
        var claimTasks = Enumerable.Range(0, simulatedWorkerCount).Select(async workerIndex =>
        {
            await using var db = CreateContext();
            var claimService = new ExecutionClaimService(db);
            for (var i = 0; i < executionCount; i++)
            {
                var claimed = await claimService.TryClaimOldestAsync($"test-worker-{workerIndex}", DateTimeOffset.UtcNow, CancellationToken.None);
                if (claimed is not null)
                {
                    claimedExecutionIds.Add(claimed.Id);
                }
            }
        });

        await Task.WhenAll(claimTasks);

        // The critical assertion: every execution was claimed exactly once, by exactly one
        // worker. If the SQL claim query's UPDLOCK/READPAST were broken, this count would be
        // higher than executionCount (a duplicate claim) - it would never be lower, so an
        // exact-match assertion on both count and distinctness is the correct proof.
        Assert.Equal(executionCount, claimedExecutionIds.Count);
        Assert.Equal(executionCount, claimedExecutionIds.Distinct().Count());

        await using var verifyDb = CreateContext();
        var stillPending = await verifyDb.JobExecutions.CountAsync(x => x.JobId == jobId && x.Status == ExecutionStatus.Pending);
        Assert.Equal(0, stillPending);
    }
}
