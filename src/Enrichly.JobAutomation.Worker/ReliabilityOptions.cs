namespace Enrichly.JobAutomation.Worker;

public sealed class ReliabilityOptions
{
    public const string SectionName = "Worker";
    public int PollIntervalSeconds { get; init; } = 2;
    public int RequestTimeoutSeconds { get; init; } = 10;
    public int MaxAttempts { get; init; } = 3;
    public int StaleExecutionMinutes { get; init; } = 5;
    public int ReclaimIntervalSeconds { get; init; } = 60;
    public int SchedulerIntervalSeconds { get; init; } = 5;
}
