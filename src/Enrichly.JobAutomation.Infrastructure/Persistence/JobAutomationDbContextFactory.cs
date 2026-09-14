using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Enrichly.JobAutomation.Infrastructure.Persistence;

public sealed class JobAutomationDbContextFactory : IDesignTimeDbContextFactory<JobAutomationDbContext>
{
    public JobAutomationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<JobAutomationDbContext>()
            .UseSqlServer("Server=MARK2\\SQLEXPRESS05;Database=EnrichlyJobAutomation;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        return new JobAutomationDbContext(options);
    }
}
