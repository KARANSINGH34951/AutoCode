# Environment Variables

Use environment variables for deployed services. JSON settings remain convenient defaults for local development.

## API

| Variable | Example | Purpose |
|---|---|---|
| `ConnectionStrings__SqlServer` | `Server=MARK2\SQLEXPRESS05;Database=EnrichlyJobAutomation;Trusted_Connection=True;TrustServerCertificate=True` | SQL Server connection string |
| `Jwt__Audience` | `Enrichly.JobAutomation` | JWT audience validation value |
| `Jwt__Key` | `replace-with-a-long-random-secret` | JWT signing key; use a strong secret in deployment |
| `Frontend__Origin` | `http://localhost:5173` | Exact frontend origin allowed by CORS |
| `ASPNETCORE_ENVIRONMENT` | `Development` | ASP.NET Core environment name |
| `ASPNETCORE_URLS` | `http://0.0.0.0:5142` | API listening URL when not using launch settings |

The API also supports standard ASP.NET Core logging configuration through the `Logging__...` hierarchy.

## Worker

| Variable | Example | Purpose |
|---|---|---|
| `ConnectionStrings__SqlServer` | `Server=MARK2\SQLEXPRESS05;Database=EnrichlyJobAutomation;Trusted_Connection=True;TrustServerCertificate=True` | SQL Server connection string |
| `Worker__PollIntervalSeconds` | `5` | Delay between claim polls |
| `Worker__RequestTimeoutSeconds` | `15` | Maximum webhook request duration |
| `Worker__MaxAttempts` | `3` | Total attempts, including the initial attempt |
| `Worker__StaleExecutionMinutes` | `5` | Age after which a Running execution is reclaimed |
| `Worker__ReclaimIntervalSeconds` | `60` | Reclaimer check interval |
| `DOTNET_ENVIRONMENT` | `Production` | Generic host environment name |

## Frontend

| Variable | Example | Purpose |
|---|---|---|
| `VITE_API_URL` | `http://localhost:5142` | Base URL used by browser API requests |

Frontend variables are embedded into the Vite build. Do not put secrets in `VITE_` variables.

## Using a Different SQL Server Instance

The default connection string targets a local SQL Server Express instance (`MARK2\SQLEXPRESS05`) with Windows Authentication. If your machine name or instance name differs, or you want to use Docker/LocalDB/Azure SQL instead, override with:

```powershell
$env:ConnectionStrings__SqlServer = 'Server=<host>\<instance-or-port>;Database=EnrichlyJobAutomation;Trusted_Connection=True;TrustServerCertificate=True'
```

(For Docker or Azure SQL, which use SQL authentication instead of Windows auth, use `User Id=...;Password=...` in place of `Trusted_Connection=True` — see `docker-compose.yml` for the Docker SA credentials.)

## Production Rules

- Do not use the development JWT key.
- Do not commit production connection strings or passwords.
- Restrict `Frontend__Origin` to the deployed frontend origin.
- Use a secret manager or platform environment variables for JWT and database credentials.
