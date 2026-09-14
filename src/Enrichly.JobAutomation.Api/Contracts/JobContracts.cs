using System.ComponentModel.DataAnnotations;
using Enrichly.JobAutomation.Domain.Entities;

namespace Enrichly.JobAutomation.Api.Contracts;

public sealed record CreateJobRequest([param: Required, StringLength(120)] string Name, [param: Required, StringLength(2048)] string TargetUrl, [param: RegularExpression("GET|POST|PUT|PATCH|DELETE")] string HttpMethod = "POST", [param: StringLength(10_000)] string? RequestBody = null, bool RunEveryHour = false);
public sealed record UpdateJobRequest([param: Required] bool IsEnabled);
public sealed record JobResponse(Guid Id, string Name, string TargetUrl, string HttpMethod, bool IsEnabled, bool IsHourly, DateTimeOffset? NextScheduledAt, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, byte[] RowVersion)
{
    public static JobResponse From(Job job) => new(job.Id, job.Name, job.TargetUrl, job.HttpMethod, job.IsEnabled, job.IsHourly, job.NextScheduledAt, job.CreatedAt, job.UpdatedAt, job.RowVersion);
}
public sealed record ExecutionResponse(Guid Id, Guid JobId, string Status, int AttemptNumber, DateTimeOffset QueuedAt, DateTimeOffset? StartedAt, DateTimeOffset? FinishedAt, string? ErrorMessage, int? ResponseStatusCode, string? ResponseBodyPreview, Guid? RetryOfExecutionId)
{
    public static ExecutionResponse From(JobExecution execution) => new(execution.Id, execution.JobId, execution.Status.ToString(), execution.AttemptNumber, execution.QueuedAt, execution.StartedAt, execution.FinishedAt, execution.ErrorMessage, execution.ResponseStatusCode, execution.ResponseBodyPreview, execution.RetryOfExecutionId);
}
