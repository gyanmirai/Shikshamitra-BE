using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShikshaMitra.EntityFrameworkCore;

namespace ShikshaMitra.Migrations;

[DbContext(typeof(ShikshaMitraDbContext))]
[Migration("20261117090000_OptimizeFeeRefundWorklist")]
public class OptimizeFeeRefundWorklist : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.CreateIndex(
			name: "IX_FeeRefundRequests_Worklist",
			table: "Finance_FeeRefundRequests",
			columns: new[] { "TenantId", "AcademicYearId", "WorkflowVersion", "Status", "CreationTime" },
			filter: "\"IsDeleted\" = false");
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropIndex(
			name: "IX_FeeRefundRequests_Worklist",
			table: "Finance_FeeRefundRequests");
	}
}
