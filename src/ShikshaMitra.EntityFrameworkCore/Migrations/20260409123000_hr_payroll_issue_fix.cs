using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
	/// <inheritdoc />
	public partial class hr_payroll_issue_fix : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropIndex(
				name: "IX_EarningComponents_TenantId_Name",
				table: "AppEarningComponents");

			migrationBuilder.DropIndex(
				name: "IX_DeductionComponents_TenantId_Name",
				table: "AppDeductionComponents");

			migrationBuilder.CreateIndex(
				name: "IX_EarningComponents_TenantId_Name",
				table: "AppEarningComponents",
				columns: new[] { "TenantId", "Name" },
				unique: true,
				filter: "\"IsDeleted\" = false");

			migrationBuilder.CreateIndex(
				name: "IX_DeductionComponents_TenantId_Name",
				table: "AppDeductionComponents",
				columns: new[] { "TenantId", "Name" },
				unique: true,
				filter: "\"IsDeleted\" = false");

			migrationBuilder.CreateIndex(
				name: "IX_ProvidentFunds_TenantId_Code",
				table: "AppProvidentFunds",
				columns: new[] { "TenantId", "Code" },
				unique: true,
				filter: "\"IsDeleted\" = false");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropIndex(
				name: "IX_ProvidentFunds_TenantId_Code",
				table: "AppProvidentFunds");

			migrationBuilder.DropIndex(
				name: "IX_EarningComponents_TenantId_Name",
				table: "AppEarningComponents");

			migrationBuilder.DropIndex(
				name: "IX_DeductionComponents_TenantId_Name",
				table: "AppDeductionComponents");

			migrationBuilder.CreateIndex(
				name: "IX_EarningComponents_TenantId_Name",
				table: "AppEarningComponents",
				columns: new[] { "TenantId", "Name" },
				unique: true);

			migrationBuilder.CreateIndex(
				name: "IX_DeductionComponents_TenantId_Name",
				table: "AppDeductionComponents",
				columns: new[] { "TenantId", "Name" },
				unique: true);
		}
	}
}
