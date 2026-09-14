using System.Data;
using System.Security.Claims;
using Enrichly.JobAutomation.Api.Contracts;
using Enrichly.JobAutomation.Api.Services;
using Enrichly.JobAutomation.Domain.Entities;
using Enrichly.JobAutomation.Domain.Enums;
using Enrichly.JobAutomation.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Enrichly.JobAutomation.Api.Controllers;

[ApiController, Authorize, Route("api/jobs")]
public sealed class JobsController(JobAutomationDbContext db, PublicWebhookUrlValidator urlValidator, ILogger<JobsController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<JobResponse>>> List(CancellationToken cancellationToken) => Ok((await db.Jobs.AsNoTracking().Where(x => x.OwnerId == UserId).OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken)).Select(JobResponse.From));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<JobResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var job = await OwnedJob(id, cancellationToken);
        return job is null ? NotFound() : Ok(JobResponse.From(job));
    }

    [HttpPost]
    public async Task<ActionResult<JobResponse>> Create(CreateJobRequest request, CancellationToken cancellationToken)
    {
        var urlError = await urlValidator.ValidateAsync(request.TargetUrl, cancellationToken);
        if (urlError is not null)
        {
            ModelState.AddModelError("targetUrl", urlError);
            return ValidationProblem(ModelState);
        }
        var now = DateTimeOffset.UtcNow;
        var job = new Job
        {
            OwnerId = UserId,
            Name = request.Name.Trim(),
            TargetUrl = request.TargetUrl.Trim(),
            HttpMethod = request.HttpMethod.ToUpperInvariant(),
            RequestBody = request.RequestBody,
            IsHourly = request.RunEveryHour,
            NextScheduledAt = request.RunEveryHour ? now.AddHours(1) : null,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Jobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = job.Id }, JobResponse.From(job));
    }

    [HttpPost("{id:guid}/run")]
    public async Task<ActionResult<ExecutionResponse>> RunNow(Guid id, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var job = await db.Jobs.SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == UserId, cancellationToken);
        if (job is null) return NotFound();
        if (!job.IsEnabled) return Conflict(new { message = "Enable this job before running it." });
        var hasActiveExecution = await db.JobExecutions.AnyAsync(x => x.JobId == id && (x.Status == ExecutionStatus.Pending || x.Status == ExecutionStatus.Running), cancellationToken);
        if (hasActiveExecution) return Conflict(new { message = "This job already has an active execution." });
        var execution = new JobExecution { JobId = id };
        db.JobExecutions.Add(execution);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Execution {ExecutionId} queued by user {UserId}.", execution.Id, UserId);
        return AcceptedAtAction(nameof(GetExecutions), new { id }, ExecutionResponse.From(execution));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<JobResponse>> Update(Guid id, UpdateJobRequest request, CancellationToken cancellationToken)
    {
        var job = await OwnedJob(id, cancellationToken);
        if (job is null) return NotFound();

        var now = DateTimeOffset.UtcNow;
        var wasEnabled = job.IsEnabled;
        job.IsEnabled = request.IsEnabled;

        // A disabled hourly job should not retain a stale due time. When it is enabled
        // again, start a fresh one-hour interval instead of immediately catching up on
        // time that elapsed while the job was disabled.
        if (job.IsHourly)
        {
            job.NextScheduledAt = request.IsEnabled
                ? (!wasEnabled ? now.AddHours(1) : job.NextScheduledAt)
                : null;
        }

        // Disabling a job stops future scheduling and also cancels executions that are
        // still waiting in the queue. A Running execution is intentionally allowed to
        // finish; cancelling an in-flight HTTP request would require cross-process
        // cancellation coordination between the API and Worker.
        if (!request.IsEnabled && wasEnabled)
        {
            await db.JobExecutions
                .Where(x => x.JobId == id && x.Status == ExecutionStatus.Pending)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Status, ExecutionStatus.Cancelled)
                    .SetProperty(x => x.FinishedAt, now)
                    .SetProperty(x => x.ErrorMessage, "Cancelled because the job was disabled."), cancellationToken);
        }

        job.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(JobResponse.From(job));
    }

    [HttpGet("{id:guid}/executions")]
    public async Task<ActionResult<IReadOnlyList<ExecutionResponse>>> GetExecutions(Guid id, CancellationToken cancellationToken)
    {
        if (await OwnedJob(id, cancellationToken) is null) return NotFound();
        var executions = await db.JobExecutions.AsNoTracking().Where(x => x.JobId == id).OrderByDescending(x => x.QueuedAt).ToListAsync(cancellationToken);
        return Ok(executions.Select(ExecutionResponse.From));
    }

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
    private Task<Job?> OwnedJob(Guid id, CancellationToken cancellationToken) => db.Jobs.SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == UserId, cancellationToken);
}
