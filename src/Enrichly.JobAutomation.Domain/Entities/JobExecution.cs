using Enrichly.JobAutomation.Domain.Enums;

namespace Enrichly.JobAutomation.Domain.Entities;

public sealed class JobExecution
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid JobId { get; set; }
    public Job Job { get; set; } = null!;
    public ExecutionStatus Status { get; set; } = ExecutionStatus.Pending;
    public int AttemptNumber { get; set; }
    public DateTimeOffset QueuedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset NextAttemptAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string? ClaimedByWorkerId { get; set; }
    public DateTimeOffset? ClaimedAt { get; set; }
    public int? ResponseStatusCode { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ResponseBodyPreview { get; set; }
    public Guid? RetryOfExecutionId { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
