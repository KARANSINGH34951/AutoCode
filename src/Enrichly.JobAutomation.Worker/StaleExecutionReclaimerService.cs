using Enrichly.JobAutomation.Domain.Enums;
using Enrichly.JobAutomation.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Enrichly.JobAutomation.Worker;

public sealed class StaleExecutionReclaimerService(
    ILogger<StaleExecutionReclaimerService> logger,
    IServiceProvider serviceProvider,
    IOptions<ReliabilityOptions> reliabilityOptions) : BackgroundService
{
    private readonly ReliabilityOptions options = reliabilityOptions.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(options.ReclaimIntervalSeconds);
        logger.LogInformation("Stale execution reclaimer started. Interval: {Interval}", interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReclaimAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Stale execution reclamation failed");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }

    private async Task ReclaimAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<JobAutomationDbContext>();
        var threshold = DateTimeOffset.UtcNow.AddMinutes(-options.StaleExecutionMinutes);
        var staleExecutions = await db.JobExecutions
            .Where(execution => execution.Status == ExecutionStatus.Running && execution.ClaimedAt < threshold)
            .ToListAsync(cancellationToken);

        foreach (var execution in staleExecutions)
        {
            execution.Status = ExecutionStatus.Pending;
            execution.ClaimedByWorkerId = null;
            execution.ClaimedAt = null;
            execution.NextAttemptAt = DateTimeOffset.UtcNow;
        }

        if (staleExecutions.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            logger.LogWarning("Reclaimed {Count} stale executions", staleExecutions.Count);
        }
    }
}