using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddDefaultLeavePolicyAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDefault",
                table: "AppEmployeeLeavePolicies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("""
                UPDATE "AppEmployeeLeavePolicies" p
                SET "IsDefault" = true
                FROM (
                    SELECT DISTINCT ON ("TenantId", "AcademicYearId") "Id"
                    FROM "AppEmployeeLeavePolicies"
                    WHERE "IsActive" = true
                      AND "Status" = 1
                      AND "IsDeleted" = false
                    ORDER BY "TenantId", "AcademicYearId", "EffectiveFrom", "PolicyName", "Id"
                ) d
                WHERE p."Id" = d."Id";
                """);

            migrationBuilder.Sql("""
                UPDATE "HR_PolicyAssignments" a
                SET "LeavePolicyId" = d."Id"
                FROM (
                    SELECT "Id", "TenantId", "AcademicYearId"
                    FROM "AppEmployeeLeavePolicies"
                    WHERE "IsDefault" = true
                      AND "IsActive" = true
                      AND "Status" = 1
                      AND "IsDeleted" = false
                ) d
                WHERE a."LeavePolicyId" IS NULL
                  AND a."AcademicYearId" = d."AcademicYearId"
                  AND (a."TenantId" = d."TenantId" OR (a."TenantId" IS NULL AND d."TenantId" IS NULL));
                """);

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX "IX_EmployeeLeavePolicies_TenantId_AcademicYearId_Default"
                ON "AppEmployeeLeavePolicies" (
                    COALESCE("TenantId", '00000000-0000-0000-0000-000000000000'::uuid),
                    "AcademicYearId"
                )
                WHERE "IsDefault" = true;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS "IX_EmployeeLeavePolicies_TenantId_AcademicYearId_Default";
                """);

            migrationBuilder.DropColumn(
                name: "IsDefault",
                table: "AppEmployeeLeavePolicies");
        }
    }
}
