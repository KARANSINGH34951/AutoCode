using Enrichly.JobAutomation.Infrastructure.Services;

namespace Enrichly.JobAutomation.Tests;

public sealed class ExecutionBackoffServiceTests
{
    private readonly ExecutionBackoffService service = new();

    [Theory]
    [InlineData(1, 10)]
    [InlineData(2, 60)]
    [InlineData(3, 300)]
    public void GetBackoffDelay_UsesAgreedSchedule(int attemptNumber, int expectedSeconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), service.GetBackoffDelay(attemptNumber));
    }

    [Theory]
    [InlineData(1, 3, true)]
    [InlineData(2, 3, true)]
    [InlineData(3, 3, false)]
    [InlineData(3, 4, true)]
    public void ShouldRetry_RespectsMaximumAttempts(int attemptNumber, int maxAttempts, bool expected)
    {
        Assert.Equal(expected, service.ShouldRetry(attemptNumber, maxAttempts));
    }
}
