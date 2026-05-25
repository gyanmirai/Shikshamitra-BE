using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    public partial class AddHrPolicyAssignmentImpactIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_HR_PolicyAssignments_AttendancePolicy_Impact",
                table: "HR_PolicyAssignments",
                columns: new[] { "TenantId", "AcademicYearId", "IsActive", "AttendancePolicyId" });

            migrationBuilder.CreateIndex(
                name: "IX_HR_PolicyAssignments_PayrollPolicy_Impact",
                table: "HR_PolicyAssignments",
                columns: new[] { "TenantId", "AcademicYearId", "IsActive", "PayrollPolicyId" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_HR_PolicyAssignments_AttendancePolicy_Impact",
                table: "HR_PolicyAssignments");

            migrationBuilder.DropIndex(
                name: "IX_HR_PolicyAssignments_PayrollPolicy_Impact",
                table: "HR_PolicyAssignments");
        }
    }
}
