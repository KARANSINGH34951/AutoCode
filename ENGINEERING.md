# ENGINEERING.md

# AutoTask — Engineering Notes

## 1. Overview

AutoTask is a job automation platform where users can create HTTP/webhook jobs, run them manually or automatically every hour, and view execution history.

The main focus of the implementation is reliable background execution, especially when multiple workers are running at the same time.

---

## 2. Architecture

```text
React + TypeScript

        |

        | HTTP

        v

   ASP.NET Core API

        |

        v

   SQL Server

        ^

        |

Background Worker(s)

        |

        v

External HTTP APIs
```

Main components

Frontend

React + TypeScript + Vite

Handles authentication, job management, execution history and scheduling controls.

API

ASP.NET Core 8 / C#

Handles authentication, authorization, validation, job creation, Run Now and retry requests.

Creates JobExecution records instead of executing HTTP requests directly.

Database

SQL Server + Entity Framework Core

Stores users, jobs and execution history.

Also acts as the durable work queue.

Worker

ASP.NET Core BackgroundService

Polls for pending executions.

Claims and executes jobs.

Handles retries and stale executions.

Creates scheduled hourly executions.

## 3. Why This Approach?

The assignment allows different technology choices, so I chose technologies I could work with effectively within the limited assignment time.

SQL Server instead of PostgreSQL

PostgreSQL was the preferred database in the assignment, but I chose SQL Server because I already have experience with it.

The relational model also fits the problem well because users, jobs and executions have clear relationships.

Database-backed queue instead of Redis/RabbitMQ

I considered using a dedicated message queue such as RabbitMQ, Redis or Azure Service Bus.

For this assignment, SQL Server was sufficient and kept the system simpler:

fewer infrastructure dependencies

durable pending executions

transactional state changes

easier local setup

For a much larger system, I would consider moving execution delivery to a dedicated queue.

Vite instead of Next.js

The frontend is primarily a client-side dashboard, so Vite keeps the frontend simple without introducing server-side rendering that isn't required for this application.

## 4. Concurrency

The most important reliability requirement is preventing two workers from executing the same execution simultaneously.

A simple:

SELECT pending job

UPDATE job to Running

is unsafe because two workers could read the same row.

The Worker therefore uses SQL Server locking when claiming an execution:

UPDLOCK + READPAST + ROWLOCK

The claim changes the execution from:

Pending → Running

atomically.

This allows multiple Worker processes to compete for work while preventing the same execution from being claimed twice.

## 5. Failure Handling

Worker crash

If a Worker crashes after claiming an execution, the execution can become stale.

A background reclaimer detects executions that have remained Running for too long and returns them to Pending so another Worker can process them.

External API failure

HTTP failures and network/timeout failures are recorded on the execution.

The current retry policy is:

Attempt 1 → retry after 10 seconds

Attempt 2 → retry after 60 seconds

Attempt 3 → permanent failure

Duplicate Run Now

If a job already has a Pending or Running execution, another Run Now request returns 409 Conflict instead of creating another active execution.

## 6. Hourly Scheduling

Jobs can optionally be configured to run every hour.

The job stores:

IsHourly

NextScheduledAt

The Worker periodically checks for due jobs and creates a Pending execution.

The scheduling operation is protected with database locking so multiple Worker instances cannot create the same scheduled execution.

If an hourly job is disabled, future scheduled executions stop.

If it is enabled again, the next run is scheduled one hour from re-enabling.

Missed hourly intervals are intentionally not replayed as a burst after downtime.

## 7. Security

Webhook URLs are validated before they can be saved.

The application:

requires HTTPS

rejects localhost/private addresses

checks DNS resolution

disables automatic HTTP redirects in the Worker

This prevents a public-looking URL from redirecting the Worker toward an internal service.

Authorization is also enforced at the data-access level so users can only access their own jobs and executions.

## 8. Testing

Testing focuses on the areas where failures would be most important:

concurrent worker execution claiming

retry behavior

authorization

duplicate Run Now requests

execution state transitions

The goal was to test important reliability behavior rather than maximize the number of superficial CRUD tests.

## 9. Trade-offs / Future Improvements

This implementation intentionally keeps the scope small.

Possible future improvements include:

cron/custom scheduling

time-zone support

dedicated message queue

configurable retry policies

custom webhook headers/authentication

real-time execution updates

richer monitoring and metrics

cancellation of already-running external requests
