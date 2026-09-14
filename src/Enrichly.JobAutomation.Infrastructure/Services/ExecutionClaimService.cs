using System.Data;
using Enrichly.JobAutomation.Domain.Entities;
using Enrichly.JobAutomation.Domain.Enums;
using Enrichly.JobAutomation.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Enrichly.JobAutomation.Infrastructure.Services;

/// <summary>Claims one due execution. The SQL statement is safe to run from many worker processes.</summary>
public sealed class ExecutionClaimService(JobAutomationDbContext db)
{
    public async Task<JobExecution?> TryClaimOldestAsync(string workerId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        const string sql = """
            ;WITH candidate AS
            (
                SELECT TOP (1) execution.Id
                FROM JobExecutions AS execution WITH (UPDLOCK, READPAST, ROWLOCK)
                INNER JOIN Jobs AS job ON job.Id = execution.JobId
                WHERE execution.Status = @pending
                  AND execution.NextAttemptAt <= @now
                  AND job.IsEnabled = 1
                ORDER BY execution.QueuedAt ASC
            )
            UPDATE execution
            SET Status = @running,
                ClaimedByWorkerId = @workerId,
                ClaimedAt = @now,
                StartedAt = COALESCE(execution.StartedAt, @now),
                AttemptNumber = execution.AttemptNumber + 1
            OUTPUT INSERTED.Id
            FROM JobExecutions AS execution
            INNER JOIN candidate ON candidate.Id = execution.Id;
            """;

        var connection = db.Database.GetDbConnection();
        var closeWhenDone = connection.State != ConnectionState.Open;
        if (closeWhenDone) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
            AddParameter(command, "@pending", (int)ExecutionStatus.Pending);
            AddParameter(command, "@running", (int)ExecutionStatus.Running);
            AddParameter(command, "@workerId", workerId);
            AddParameter(command, "@now", now);
            Guid executionId;
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                if (!await reader.ReadAsync(cancellationToken)) return null;
                executionId = reader.GetGuid(0);
            }

            return await db.JobExecutions.Include(x => x.Job).SingleAsync(x => x.Id == executionId, cancellationToken);
        }
        finally
        {
            if (closeWhenDone) await connection.CloseAsync();
        }
    }

    private static void AddParameter(System.Data.Common.DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
