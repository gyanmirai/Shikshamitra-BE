using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using ShikshaMitra.EntityFrameworkCore;

#nullable disable

namespace ShikshaMitra.Migrations
{
    [DbContext(typeof(ShikshaMitraDbContext))]
    [Migration("20260531000200_AddHrDashboardRefreshRunAndLeaveDateOnly")]
    public partial class AddHrDashboardRefreshRunAndLeaveDateOnly : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "EndDateOnly",
                table: "AppEmployeeLeaves",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<DateOnly>(
                name: "StartDateOnly",
                table: "AppEmployeeLeaves",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.Sql("""
                UPDATE "AppEmployeeLeaves"
                SET
                    "StartDateOnly" = ("StartDate" AT TIME ZONE 'Asia/Kolkata')::date,
                    "EndDateOnly" = ("EndDate" AT TIME ZONE 'Asia/Kolkata')::date
                WHERE "StartDateOnly" = DATE '0001-01-01'
                   OR "EndDateOnly" = DATE '0001-01-01';
                """);

            migrationBuilder.CreateTable(
                name: "HR_DashboardRefreshRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantCount = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_DashboardRefreshRuns", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeLeaves_Dashboard_DateScope",
                table: "AppEmployeeLeaves",
                columns: new[] { "TenantId", "Status", "StartDateOnly", "EndDateOnly", "EmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_HR_DashboardRefreshRuns_RunKey",
                table: "HR_DashboardRefreshRuns",
                column: "RunKey",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HR_DashboardRefreshRuns");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeLeaves_Dashboard_DateScope",
                table: "AppEmployeeLeaves");

            migrationBuilder.DropColumn(
                name: "EndDateOnly",
                table: "AppEmployeeLeaves");

            migrationBuilder.DropColumn(
                name: "StartDateOnly",
                table: "AppEmployeeLeaves");
        }
    }
}
