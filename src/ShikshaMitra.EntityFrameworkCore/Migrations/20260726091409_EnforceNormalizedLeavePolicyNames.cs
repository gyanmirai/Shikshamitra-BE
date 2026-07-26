using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class EnforceNormalizedLeavePolicyNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EmployeeLeavePolicies_TenantId_AcademicYearId_PolicyName",
                table: "AppEmployeeLeavePolicies");

            migrationBuilder.AddColumn<string>(
                name: "NormalizedPolicyName",
                table: "AppEmployeeLeavePolicies",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                """
                UPDATE "AppEmployeeLeavePolicies"
                SET "NormalizedPolicyName" = UPPER(BTRIM("PolicyName"));

                DO $migration$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "AppEmployeeLeavePolicies"
                        WHERE "TenantId" IS NOT NULL
                          AND "Status" <> 2
                        GROUP BY "TenantId", "AcademicYearId", "NormalizedPolicyName"
                        HAVING COUNT(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'Cannot enforce case-insensitive leave policy names because current policies share a name in the same tenant and academic year. Rename or archive each case-insensitive duplicate before retrying the migration.';
                    END IF;
                END
                $migration$;

                ALTER TABLE "AppEmployeeLeavePolicies"
                ALTER COLUMN "NormalizedPolicyName" DROP DEFAULT;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeLeavePolicies_TenantId_AcademicYearId_PolicyName",
                table: "AppEmployeeLeavePolicies",
                columns: new[] { "TenantId", "AcademicYearId", "NormalizedPolicyName" },
                unique: true,
                filter: "\"Status\" <> 2");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EmployeeLeavePolicies_TenantId_AcademicYearId_PolicyName",
                table: "AppEmployeeLeavePolicies");

            migrationBuilder.DropColumn(
                name: "NormalizedPolicyName",
                table: "AppEmployeeLeavePolicies");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeLeavePolicies_TenantId_AcademicYearId_PolicyName",
                table: "AppEmployeeLeavePolicies",
                columns: new[] { "TenantId", "AcademicYearId", "PolicyName" },
                unique: true,
                filter: "\"Status\" <> 2");
        }
    }
}
