using System.Security.Claims;
using Enrichly.JobAutomation.Api.Contracts;
using Enrichly.JobAutomation.Domain.Entities;
using Enrichly.JobAutomation.Domain.Enums;
using Enrichly.JobAutomation.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Enrichly.JobAutomation.Api.Controllers;

[ApiController, Authorize, Route("api/executions")]
public sealed class ExecutionsController(JobAutomationDbContext db) : ControllerBase
{
    [HttpPost("{id:guid}/retry")]
    public async Task<ActionResult<ExecutionResponse>> Retry(Guid id, CancellationToken cancellationToken)
    {
        var source = await db.JobExecutions.Include(x => x.Job).SingleOrDefaultAsync(x => x.Id == id && x.Job.OwnerId == UserId, cancellationToken);
        if (source is null) return NotFound();
        if (source.Status != ExecutionStatus.Failed) return Conflict(new { message = "Only failed executions can be retried." });
        var retry = new JobExecution { JobId = source.JobId, RetryOfExecutionId = source.Id };
        db.JobExecutions.Add(retry);
        await db.SaveChangesAsync(cancellationToken);
        return Accepted(ExecutionResponse.From(retry));
    }

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
}
