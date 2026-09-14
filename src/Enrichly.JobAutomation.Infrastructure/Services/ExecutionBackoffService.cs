namespace Enrichly.JobAutomation.Infrastructure.Services;

public sealed class ExecutionBackoffService
{
    // Explicit delays keep the agreed policy visible: 10 seconds, 60 seconds, then 5 minutes.
    private static readonly TimeSpan[] BackoffDelays =
    [
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(60),
        TimeSpan.FromMinutes(5)
    ];

    public bool ShouldRetry(int attemptNumber, int maxAttempts = 3) => attemptNumber < maxAttempts;

    public TimeSpan GetBackoffDelay(int attemptNumber)
    {
        var index = Math.Clamp(attemptNumber - 1, 0, BackoffDelays.Length - 1);
        return BackoffDelays[index];
    }
}