using Enrichly.JobAutomation.Domain.Enums;

namespace Enrichly.JobAutomation.Domain.Entities;

public sealed class Job
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public User Owner { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public JobType Type { get; set; } = JobType.HttpWebhook;
    public string TargetUrl { get; set; } = string.Empty;
    public string HttpMethod { get; set; } = "POST";
    public string? RequestBody { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsHourly { get; set; }
    public DateTimeOffset? NextScheduledAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public ICollection<JobExecution> Executions { get; set; } = new List<JobExecution>();
}
