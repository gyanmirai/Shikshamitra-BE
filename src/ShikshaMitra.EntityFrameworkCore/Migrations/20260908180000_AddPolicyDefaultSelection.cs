using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShikshaMitra.EntityFrameworkCore;

#nullable disable

namespace ShikshaMitra.Migrations;

[DbContext(typeof(ShikshaMitraDbContext))]
[Migration("20260908180000_AddPolicyDefaultSelection")]
public partial class AddPolicyDefaultSelection : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropIndex(
			name: "IX_Finance_CollectionPolicies_Tenant_AcademicYear_Default",
			table: "Finance_CollectionPolicies");

		migrationBuilder.AddColumn<bool>(
			name: "IsDefault",
			table: "Finance_ConcessionPolicies",
			type: "boolean",
			nullable: false,
			defaultValue: false);

		migrationBuilder.AddColumn<bool>(
			name: "IsDefault",
			table: "Finance_RefundPolicies",
			type: "boolean",
			nullable: false,
			defaultValue: false);

		migrationBuilder.CreateIndex(
			name: "IX_Finance_CollectionPolicies_Tenant_AcademicYear_Default",
			table: "Finance_CollectionPolicies",
			columns: new[] { "TenantId", "AcademicYearId", "IsDefault" },
			unique: true,
			filter: "\"IsDeleted\" = false AND \"Status\" = 1 AND \"IsDefault\" = true");

		migrationBuilder.CreateIndex(
			name: "IX_Finance_ConcessionPolicies_ActiveDefault",
			table: "Finance_ConcessionPolicies",
			columns: new[] { "TenantId", "AcademicYearId", "IsDefault" },
			unique: true,
			filter: "\"IsDeleted\" = false AND \"Status\" = 1 AND \"IsDefault\" = true");

		migrationBuilder.CreateIndex(
			name: "IX_Finance_RefundPolicies_ActiveDefault",
			table: "Finance_RefundPolicies",
			columns: new[] { "TenantId", "AcademicYearId", "IsDefault" },
			unique: true,
			filter: "\"IsDeleted\" = false AND \"Status\" = 1 AND \"IsDefault\" = true");
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropIndex(
			name: "IX_Finance_CollectionPolicies_Tenant_AcademicYear_Default",
			table: "Finance_CollectionPolicies");

		migrationBuilder.DropIndex(
			name: "IX_Finance_ConcessionPolicies_ActiveDefault",
			table: "Finance_ConcessionPolicies");

		migrationBuilder.DropIndex(
			name: "IX_Finance_RefundPolicies_ActiveDefault",
			table: "Finance_RefundPolicies");

		migrationBuilder.DropColumn(
			name: "IsDefault",
			table: "Finance_ConcessionPolicies");

		migrationBuilder.DropColumn(
			name: "IsDefault",
			table: "Finance_RefundPolicies");

		migrationBuilder.CreateIndex(
			name: "IX_Finance_CollectionPolicies_Tenant_AcademicYear_Default",
			table: "Finance_CollectionPolicies",
			columns: new[] { "TenantId", "AcademicYearId", "IsDefault" },
			unique: true,
			filter: "\"IsDefault\" = true");
	}
}
