using System.Text;
using Enrichly.JobAutomation.Domain.Entities;
using Enrichly.JobAutomation.Domain.Enums;
using Enrichly.JobAutomation.Infrastructure.Persistence;
using Enrichly.JobAutomation.Infrastructure.Services;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;

namespace Enrichly.JobAutomation.Worker;

public sealed class Worker(
    ILogger<Worker> logger,
    IServiceProvider serviceProvider,
    IOptions<ReliabilityOptions> reliabilityOptions,
    IHttpClientFactory httpClientFactory) : BackgroundService
{
    private readonly ReliabilityOptions options = reliabilityOptions.Value;
    private readonly string workerId = Environment.MachineName;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollInterval = TimeSpan.FromSeconds(options.PollIntervalSeconds);
        logger.LogInformation("Worker {WorkerId} started. Poll interval: {PollInterval}", workerId, pollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var claimService = scope.ServiceProvider.GetRequiredService<ExecutionClaimService>();
                var backoffService = scope.ServiceProvider.GetRequiredService<ExecutionBackoffService>();
                var db = scope.ServiceProvider.GetRequiredService<JobAutomationDbContext>();
                var execution = await claimService.TryClaimOldestAsync(workerId, DateTimeOffset.UtcNow, stoppingToken);

                if (execution is not null)
                {
                    logger.LogInformation("Worker {WorkerId} claimed execution {ExecutionId} for job {JobId}", workerId, execution.Id, execution.JobId);
                    await ExecuteJobAsync(execution, db, backoffService, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Worker {WorkerId} encountered an error and will continue polling", workerId);
            }

            await Task.Delay(pollInterval, stoppingToken);
        }

        logger.LogInformation("Worker {WorkerId} stopped", workerId);
    }

    private async Task ExecuteJobAsync(JobExecution execution, JobAutomationDbContext db, ExecutionBackoffService backoffService, CancellationToken stoppingToken)
    {
        try
        {
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            timeoutSource.CancelAfter(TimeSpan.FromSeconds(options.RequestTimeoutSeconds));
            using var request = new HttpRequestMessage
            {
                Method = new HttpMethod(execution.Job.HttpMethod),
                RequestUri = new Uri(execution.Job.TargetUrl),
                Content = execution.Job.RequestBody is null ? null : new StringContent(execution.Job.RequestBody, Encoding.UTF8, "application/json")
            };

            // The named client disables redirects so a public URL cannot redirect the worker into a private network target.
            var httpClient = httpClientFactory.CreateClient("webhooks");
            using var response = await httpClient.SendAsync(request, timeoutSource.Token);
            execution.ResponseStatusCode = (int)response.StatusCode;
            var responseBody = await response.Content.ReadAsStringAsync(timeoutSource.Token);
            execution.ResponseBodyPreview = responseBody.Length > 4000 ? responseBody[..4000] : responseBody;
            execution.FinishedAt = DateTimeOffset.UtcNow;

            if (response.IsSuccessStatusCode)
            {
                execution.Status = ExecutionStatus.Completed;
                execution.ErrorMessage = null;
                logger.LogInformation("Execution {ExecutionId} completed with HTTP {StatusCode}", execution.Id, execution.ResponseStatusCode);
            }
            else
            {
                HandleFailure(execution, $"HTTP {response.StatusCode}: {response.ReasonPhrase}", backoffService, retry: true);
            }
        }
        catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
        {
            execution.FinishedAt = DateTimeOffset.UtcNow;
            HandleFailure(execution, "HTTP request timed out", backoffService, retry: true);
        }
        catch (HttpRequestException exception)
        {
            execution.FinishedAt = DateTimeOffset.UtcNow;
            HandleFailure(execution, $"Network error: {exception.Message}", backoffService, retry: true);
        }
        catch (Exception exception)
        {
            execution.FinishedAt = DateTimeOffset.UtcNow;
            HandleFailure(execution, $"Unexpected error: {exception.Message}", backoffService, retry: false);
            logger.LogError(exception, "Unexpected error during execution {ExecutionId}", execution.Id);
        }

        await db.SaveChangesAsync(stoppingToken);
    }

    private void HandleFailure(JobExecution execution, string errorMessage, ExecutionBackoffService backoffService, bool retry)
    {
        execution.ErrorMessage = errorMessage;
        if (retry && backoffService.ShouldRetry(execution.AttemptNumber, options.MaxAttempts))
        {
            execution.Status = ExecutionStatus.Pending;
            execution.NextAttemptAt = DateTimeOffset.UtcNow.Add(backoffService.GetBackoffDelay(execution.AttemptNumber));
            logger.LogWarning("Execution {ExecutionId} failed on attempt {AttemptNumber}; retry scheduled for {NextAttemptAt}: {Error}", execution.Id, execution.AttemptNumber, execution.NextAttemptAt, errorMessage);
            return;
        }

        execution.Status = ExecutionStatus.Failed;
        logger.LogError("Execution {ExecutionId} failed permanently: {Error}", execution.Id, errorMessage);
    }
}
