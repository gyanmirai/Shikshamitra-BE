using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    [Migration("20260606120000_add_payroll_month_locks")]
    public partial class add_payroll_month_locks : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HR_PayrollMonthLocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    MonthYear = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_PayrollMonthLocks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HR_PayrollMonthLocks_ExpiresAt",
                table: "HR_PayrollMonthLocks",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_HR_PayrollMonthLocks_Tenant_Month",
                table: "HR_PayrollMonthLocks",
                columns: new[] { "TenantId", "MonthYear" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HR_PayrollMonthLocks");
        }
    }
}
