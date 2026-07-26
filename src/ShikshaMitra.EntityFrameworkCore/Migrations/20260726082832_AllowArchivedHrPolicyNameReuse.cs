using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AllowArchivedHrPolicyNameReuse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_HR_WorkCalendarPolicies_Tenant_AcademicYear_Name",
                table: "HR_WorkCalendarPolicies");

            migrationBuilder.DropIndex(
                name: "IX_HR_PayrollPolicies_Tenant_AcademicYear_Name",
                table: "HR_PayrollPolicies");

            migrationBuilder.DropIndex(
                name: "IX_HR_AttendancePolicies_Tenant_AcademicYear_Name",
                table: "HR_AttendancePolicies");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeLeavePolicies_TenantId_AcademicYearId_PolicyName",
                table: "AppEmployeeLeavePolicies");

            migrationBuilder.CreateIndex(
                name: "IX_HR_WorkCalendarPolicies_Tenant_AcademicYear_Name",
                table: "HR_WorkCalendarPolicies",
                columns: new[] { "TenantId", "AcademicYearId", "Name" },
                unique: true,
                filter: "\"Status\" <> 2");

            migrationBuilder.CreateIndex(
                name: "IX_HR_PayrollPolicies_Tenant_AcademicYear_Name",
                table: "HR_PayrollPolicies",
                columns: new[] { "TenantId", "AcademicYearId", "Name" },
                unique: true,
                filter: "\"Status\" <> 2");

            migrationBuilder.CreateIndex(
                name: "IX_HR_AttendancePolicies_Tenant_AcademicYear_Name",
                table: "HR_AttendancePolicies",
                columns: new[] { "TenantId", "AcademicYearId", "Name" },
                unique: true,
                filter: "\"Status\" <> 2");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeLeavePolicies_TenantId_AcademicYearId_PolicyName",
                table: "AppEmployeeLeavePolicies",
                columns: new[] { "TenantId", "AcademicYearId", "PolicyName" },
                unique: true,
                filter: "\"Status\" <> 2");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $migration$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "HR_WorkCalendarPolicies"
                        WHERE "TenantId" IS NOT NULL
                        GROUP BY "TenantId", "AcademicYearId", "Name"
                        HAVING COUNT(*) > 1
                    ) OR EXISTS (
                        SELECT 1
                        FROM "HR_PayrollPolicies"
                        WHERE "TenantId" IS NOT NULL
                        GROUP BY "TenantId", "AcademicYearId", "Name"
                        HAVING COUNT(*) > 1
                    ) OR EXISTS (
                        SELECT 1
                        FROM "HR_AttendancePolicies"
                        WHERE "TenantId" IS NOT NULL
                        GROUP BY "TenantId", "AcademicYearId", "Name"
                        HAVING COUNT(*) > 1
                    ) OR EXISTS (
                        SELECT 1
                        FROM "AppEmployeeLeavePolicies"
                        WHERE "TenantId" IS NOT NULL
                        GROUP BY "TenantId", "AcademicYearId", "PolicyName"
                        HAVING COUNT(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'Cannot roll back AllowArchivedHrPolicyNameReuse because archived and current policies now share a name in the same tenant and academic year. Rename or remove each archived duplicate before retrying the rollback.';
                    END IF;
                END
                $migration$;
                """);

            migrationBuilder.DropIndex(
                name: "IX_HR_WorkCalendarPolicies_Tenant_AcademicYear_Name",
                table: "HR_WorkCalendarPolicies");

            migrationBuilder.DropIndex(
                name: "IX_HR_PayrollPolicies_Tenant_AcademicYear_Name",
                table: "HR_PayrollPolicies");

            migrationBuilder.DropIndex(
                name: "IX_HR_AttendancePolicies_Tenant_AcademicYear_Name",
                table: "HR_AttendancePolicies");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeLeavePolicies_TenantId_AcademicYearId_PolicyName",
                table: "AppEmployeeLeavePolicies");

            migrationBuilder.CreateIndex(
                name: "IX_HR_WorkCalendarPolicies_Tenant_AcademicYear_Name",
                table: "HR_WorkCalendarPolicies",
                columns: new[] { "TenantId", "AcademicYearId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HR_PayrollPolicies_Tenant_AcademicYear_Name",
                table: "HR_PayrollPolicies",
                columns: new[] { "TenantId", "AcademicYearId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HR_AttendancePolicies_Tenant_AcademicYear_Name",
                table: "HR_AttendancePolicies",
                columns: new[] { "TenantId", "AcademicYearId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeLeavePolicies_TenantId_AcademicYearId_PolicyName",
                table: "AppEmployeeLeavePolicies",
                columns: new[] { "TenantId", "AcademicYearId", "PolicyName" },
                unique: true);
        }
    }
}
