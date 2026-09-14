using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Enrichly.JobAutomation.Infrastructure.Persistence.Migrations;

public partial class AddHourlyScheduling : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsHourly",
            table: "Jobs",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "NextScheduledAt",
            table: "Jobs",
            type: "datetimeoffset",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Jobs_IsHourly_NextScheduledAt",
            table: "Jobs",
            columns: new[] { "IsHourly", "NextScheduledAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Jobs_IsHourly_NextScheduledAt",
            table: "Jobs");

        migrationBuilder.DropColumn(
            name: "IsHourly",
            table: "Jobs");

        migrationBuilder.DropColumn(
            name: "NextScheduledAt",
            table: "Jobs");
    }
}
