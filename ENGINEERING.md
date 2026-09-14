# AutoTask — Engineering Notes

## Overview

AutoTask is a small job automation platform where users can create HTTP/webhook jobs, run them manually, schedule them to run every hour, and view their execution history.

The main focus was making background execution reliable, especially when multiple workers are running or when something fails during execution.

## Architecture

The application is split into four main parts:

```text
React + TypeScript
        |
        v
ASP.NET Core API
        |
        v
    SQL Server
        ^
        |
Background Worker
        |
        v
External HTTP APIs
```

- **Frontend:** React + TypeScript + Vite
- **API:** ASP.NET Core 8 / C#
- **Database:** SQL Server + Entity Framework Core
- **Worker:** ASP.NET Core BackgroundService

The API handles authentication, authorization, job management, validation, and creating executions.

The API does not execute the external HTTP request directly. Instead, it creates a `JobExecution` with a `Pending` status. The background worker then picks it up and executes it.

This keeps the API responsive and separates user requests from background processing.

## Why This Approach?

The assignment preferred Next.js, PostgreSQL, and a dedicated background processing system, but allowed other choices.

I chose technologies I was already comfortable with so I could spend more time on the reliability and concurrency parts of the problem.

### SQL Server

I used SQL Server instead of PostgreSQL because I already have experience with it.

The relational model also fits the application well because users, jobs, and executions have clear relationships.

### Database-backed Queue

For this assignment, I decided that using SQL Server as a durable work queue was enough. It keeps the architecture simpler while still allowing pending executions to survive worker restarts.

For a larger production system with higher throughput, I would consider using a dedicated message queue.

### Vite

I used Vite instead of Next.js because this application is mainly a client-side dashboard and does not need server-side rendering.

## Execution and Concurrency

One of the main reliability concerns was making sure two workers do not execute the same execution.

A simple approach such as:

```text
SELECT pending execution
UPDATE execution to Running
```

can have a race condition where two workers select the same execution.

The worker therefore uses SQL Server locking when claiming work:

```text
UPDLOCK + READPAST + ROWLOCK
```

The execution is changed from:

```text
Pending -> Running
```

as part of the claim operation.

This allows multiple workers to safely compete for pending executions without both processing the same execution.

## Duplicate "Run Now"

The API also prevents multiple active executions from being created when a user clicks `Run Now` more than once.

If a job already has a `Pending` or `Running` execution, another request returns:

```text
409 Conflict
```

instead of creating another active execution.

The check and creation are handled transactionally so concurrent requests cannot simply bypass the check.

## Failures and Retries

External HTTP requests can fail because of timeouts, network problems, or HTTP errors.

The current retry policy is:

```text
Attempt 1 -> retry after 10 seconds
Attempt 2 -> retry after 60 seconds
Attempt 3 -> mark as failed
```

Execution history records the attempt number, error information, HTTP status code, and retry information.

If a worker crashes after claiming an execution, a stale execution reclaimer can return a long-running execution from:

```text
Running -> Pending
```

so another worker can process it.

## Hourly Scheduling

Jobs can optionally run automatically every hour.

The job stores:

```text
IsHourly
NextScheduledAt
```

The worker checks for due jobs and creates pending executions.

The scheduling operation also uses database locking so multiple workers do not create the same scheduled execution.

I intentionally do not replay every missed hourly run after downtime. If the worker is unavailable for several hours, the application continues with the next scheduled run instead of creating a burst of old executions.

## Security

Because users provide URLs that the worker will call, the application validates webhook URLs.

It:

- Requires HTTPS
- Rejects localhost/private addresses
- Checks DNS resolution
- Disables automatic redirects in the worker

This helps prevent a public-looking URL from being used to reach an internal service through redirects.

Authorization is also enforced so users can only access their own jobs and executions.

## Testing

I focused testing on the areas where failures would have the biggest impact:

- Concurrent execution claiming
- Retry behavior
- Authorization
- Duplicate `Run Now` requests
- Execution state transitions

The goal was to test the important reliability behavior rather than maximize the number of basic CRUD tests.

## Trade-offs and Future Improvements

The implementation was intentionally kept small because of the assignment time limit.

If I continued developing it, I would consider:

- Cron/custom scheduling
- Time-zone support
- A dedicated message queue
- Configurable retry policies
- Custom webhook authentication/headers
- Better monitoring and metrics
- Real-time execution updates
- Cancellation of already-running requests

The current design focuses on the core job workflow while keeping the infrastructure relatively simple.
