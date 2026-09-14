# Enrichly Job Automation

A small job automation platform for creating HTTP webhook jobs, triggering them manually, and inspecting execution outcomes. The application separates the API from a polling Worker so execution reliability, retries, and crash recovery are explicit parts of the design.

## Stack

- ASP.NET Core 8 Web API
- .NET 8 Background Worker
- SQL Server + Entity Framework Core
- React, TypeScript, and Vite
- JWT bearer authentication

## Repository Layout

```text
src/Enrichly.JobAutomation.Api             REST API and authentication
src/Enrichly.JobAutomation.Domain          Entities and enums
src/Enrichly.JobAutomation.Infrastructure  EF Core persistence and claim logic
src/Enrichly.JobAutomation.Worker          Polling webhook executor
frontend                                   React/Vite client
docker-compose.yml                         Local SQL Server
```

## Quickstart

### Prerequisites

- .NET 8 SDK
- Node.js and npm
- SQL Server 2022, Docker, or Windows LocalDB

### Start SQL Server

The default local target is a **SQL Server Express** instance (`MARK2\SQLEXPRESS05`), using Windows Authentication (`Trusted_Connection=True`) — no password needed. This is already the default in `appsettings.json` for the API and Worker, and the design-time EF factory used by `dotnet ef`.

Confirm the instance is running before continuing:

```powershell
Get-Service | Where-Object {$_.Name -like "*SQLEXPRESS05*"}
```

If it's stopped, start it (adjust the service name to match what the command above returns):

```powershell
Start-Service 'MSSQL$SQLEXPRESS05'
```

No connection string override is needed if your instance name matches `MARK2\SQLEXPRESS05`. If it differs, override it directly:

```powershell
$env:ConnectionStrings__SqlServer = 'Server=<YOUR-MACHINE>\<YOUR-INSTANCE>;Database=EnrichlyJobAutomation;Trusted_Connection=True;TrustServerCertificate=True'
```

**Docker Compose alternative** (`docker-compose.yml`) is still provided and kept up to date for deployment parity and CI-style runs, but is not the primary local path right now. To use it instead, start it and then override the connection string to the Docker instance's credentials (see `docker-compose.yml` for the SA password):

```powershell
docker compose up -d sqlserver
$env:ConnectionStrings__SqlServer = 'Server=localhost,1433;Database=EnrichlyJobAutomation;User Id=sa;Password=<see docker-compose.yml>;TrustServerCertificate=True'
```

### Apply the database migration

From the repository root:

```powershell
dotnet ef database update `
  --project .\src\Enrichly.JobAutomation.Infrastructure `
  --startup-project .\src\Enrichly.JobAutomation.Api
```

### Run the API

```powershell
dotnet run --project .\src\Enrichly.JobAutomation.Api --launch-profile http
```

Useful local URLs:

- API: `http://localhost:5142`
- Swagger UI: `http://localhost:5142/swagger`
- OpenAPI JSON: `http://localhost:5142/swagger/v1/swagger.json`

### Run the Worker

In a second terminal:

```powershell
dotnet run --project .\src\Enrichly.JobAutomation.Worker
```

### Run the frontend

In a third terminal:

```powershell
cd frontend
npm install
npm run dev
```

Open `http://localhost:5173`.

Set `VITE_API_URL` when the API is not running on the default local URL. See [docs/ENVIRONMENT.md](docs/ENVIRONMENT.md).

## Using the Application

1. Register an account.
2. Create a job with a public HTTPS webhook, such as `https://httpbin.org/post`.
3. Select the job and click **Run**.
4. The Worker claims the execution and updates the execution history.
5. Select a failed execution to see the error and response code, then click **Retry failed execution**.
6. Use the shield control to enable or disable the job.

## Reliability Design

The Worker claims work using SQL Server row locks (`UPDLOCK`, `READPAST`, and `ROWLOCK`). It only claims pending executions whose `NextAttemptAt` has arrived and whose Job is enabled. Requests have a 15-second timeout and retry at 10 seconds, 60 seconds, and 5 minutes. A stale execution reclaimer returns executions left in `Running` after a worker crash to `Pending`.

The frontend polls execution history every four seconds only while an execution is `Pending` or `Running`.

## Build and Validation

```powershell
dotnet build .\Enrichly.JobAutomation.sln
cd frontend
npm run build
```

The live smoke flow has been validated with LocalDB (an earlier local setup; SQL Server Express is now the default — see "Start SQL Server" above), the API, Worker, browser UI, and `https://httpbin.org/post`. It covered registration, job creation, execution completion, captured response output, and disabling a job. The failure/retry path was separately validated against `https://httpbin.org/status/404`, including a manual Retry click through the actual UI.

Integration tests (`ApiIntegrationTests`, `ExecutionClaimConcurrencyTests`) default to the same SQL Server Express instance (with a separate throwaway test database) and have not yet been run since being added — see [ENGINEERING.md](ENGINEERING.md) "Testing" section.

## Deployment Plan

The intended deployment uses three Render services and Azure SQL:

1. Create an Azure SQL logical server and database.
2. Apply the initial EF migration against Azure SQL.
3. Add the Render outbound IP range to the Azure SQL firewall. Render starter plans may use dynamic egress IPs, so this is a documented operational trade-off.
4. Deploy the API as a Render Web Service using the API project and `dotnet run` or a published DLL.
5. Deploy the Worker as a Render Background Worker using the Worker project.
6. Deploy `frontend` as a Render Static Site with `npm install` and `npm run build`, publishing `frontend/dist`.
7. Set `VITE_API_URL` to the deployed API URL and `Frontend__Origin` on the API to the deployed frontend origin.
8. Set the production SQL connection string and a strong JWT key through service environment variables.
9. Verify registration, job creation, successful webhook execution, failure/retry behavior, Swagger, and ownership isolation after deployment.

The deployment artifacts are [render.yaml](render.yaml), [deploy/api.Dockerfile](deploy/api.Dockerfile), and [deploy/worker.Dockerfile](deploy/worker.Dockerfile). Docker image builds were not run locally because Docker is not installed in the current environment.

See [ENGINEERING.md](ENGINEERING.md) for the main design decisions and known limitations.
