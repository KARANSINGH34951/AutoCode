using Enrichly.JobAutomation.Domain.Entities;
using Enrichly.JobAutomation.Domain.Enums;

namespace Enrichly.JobAutomation.Tests;

public sealed class JobExecutionTests
{
    [Fact]
    public void NewExecution_IsPendingAndImmediatelyEligible()
    {
        var execution = new JobExecution();

        Assert.Equal(ExecutionStatus.Pending, execution.Status);
        Assert.Equal(0, execution.AttemptNumber);
        Assert.True(execution.NextAttemptAt <= DateTimeOffset.UtcNow);
        Assert.Null(execution.FinishedAt);
        Assert.Null(execution.ErrorMessage);
    }
}
