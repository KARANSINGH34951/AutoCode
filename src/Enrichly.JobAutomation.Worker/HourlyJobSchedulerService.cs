using Enrichly.JobAutomation.Domain.Entities;
using Enrichly.JobAutomation.Domain.Enums;
using Enrichly.JobAutomation.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Enrichly.JobAutomation.Worker;

/// <summary>Creates one pending execution when an hourly job becomes due.</summary>
/// <remarks>
/// This service is safe to run in every Worker process. A transaction with an
/// UPDLOCK on the Job row ensures that two scheduler instances cannot create the same
/// hourly execution. The scheduler only creates work; the normal Worker still owns
/// execution, retries, and failure recovery.
/// </remarks>
public sealed class HourlyJobSchedulerService(
    ILogger<HourlyJobSchedulerService> logger,
    IServiceProvider serviceProvider,
    IOptions<ReliabilityOptions> reliabilityOptions) : BackgroundService
{
    private readonly ReliabilityOptions options = reliabilityOptions.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(options.SchedulerIntervalSeconds);
        logger.LogInformation("Hourly scheduler started. Check interval: {Interval}", interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ScheduleDueJobsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Hourly job scheduling failed; will retry on the next check");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }

    private async Task ScheduleDueJobsAsync(CancellationToken cancellationToken)
    {
        // Process a bounded batch so a temporary outage does not cause an unbounded
        // catch-up loop after the scheduler comes back.
        for (var i = 0; i < 100 && !cancellationToken.IsCancellationRequested; i++)
        {
            if (!await ScheduleOneDueJobAsync(cancellationToken))
                break;
        }
    }

    private async Task<bool> ScheduleOneDueJobAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<JobAutomationDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // Keep the transaction at READ COMMITTED. UPDLOCK reserves the selected job row
        // until commit, while READPAST lets another scheduler skip a row already being
        // scheduled instead of blocking. This is compatible with the worker claim query,
        // which also uses READPAST.
        var job = await db.Jobs
            .FromSqlInterpolated($"SELECT TOP (1) * FROM Jobs WITH (UPDLOCK, READPAST, ROWLOCK) WHERE IsHourly = 1 AND IsEnabled = 1 AND NextScheduledAt IS NOT NULL AND NextScheduledAt <= {DateTimeOffset.UtcNow} ORDER BY NextScheduledAt ASC")
            .SingleOrDefaultAsync(cancellationToken);

        if (job is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        var hasActiveExecution = await db.JobExecutions.AnyAsync(
            x => x.JobId == job.Id && (x.Status == ExecutionStatus.Pending || x.Status == ExecutionStatus.Running),
            cancellationToken);

        if (!hasActiveExecution)
        {
            db.JobExecutions.Add(new JobExecution { JobId = job.Id });
            logger.LogInformation("Hourly schedule queued execution for job {JobId}", job.Id);
        }
        else
        {
            logger.LogInformation("Hourly schedule for job {JobId} was due while an execution was active; advancing to the next hour without creating a duplicate", job.Id);
        }

        // Advance from the current due time rather than from the scheduler's actual
        // polling time, then skip missed intervals after downtime. This prevents a
        // deployment/outage from creating a large burst of catch-up executions.
        var next = job.NextScheduledAt!.Value.AddHours(1);
        var now = DateTimeOffset.UtcNow;
        while (next <= now)
            next = next.AddHours(1);
        job.NextScheduledAt = next;
        job.UpdatedAt = now;

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
