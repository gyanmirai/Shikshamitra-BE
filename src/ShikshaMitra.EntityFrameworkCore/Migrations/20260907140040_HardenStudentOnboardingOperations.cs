using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class HardenStudentOnboardingOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppStudentOnboardingPolicies_TenantId_AcademicYearId_Offeri~",
                table: "AppStudentOnboardingPolicies");

            migrationBuilder.AddColumn<DateTime>(
                name: "DueAt",
                table: "AppStudentOnboardingCases",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP + INTERVAL '72 hours'");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingPolicy_AcademicYearDefault",
                table: "AppStudentOnboardingPolicies",
                columns: new[] { "TenantId", "AcademicYearId" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"OfferingId\" IS NULL AND \"StandardId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingPolicy_OfferingStandardOverride",
                table: "AppStudentOnboardingPolicies",
                columns: new[] { "TenantId", "AcademicYearId", "OfferingId", "StandardId" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"OfferingId\" IS NOT NULL AND \"StandardId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentOnboardingCases_TenantId_AcademicYearId_DivisionI~",
                table: "AppStudentOnboardingCases",
                columns: new[] { "TenantId", "AcademicYearId", "DivisionId", "RollNumber" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"DivisionId\" IS NOT NULL AND \"RollNumber\" IS NOT NULL AND \"Status\" IN (0, 1, 2, 3)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OnboardingPolicy_AcademicYearDefault",
                table: "AppStudentOnboardingPolicies");

            migrationBuilder.DropIndex(
                name: "IX_OnboardingPolicy_OfferingStandardOverride",
                table: "AppStudentOnboardingPolicies");

            migrationBuilder.DropIndex(
                name: "IX_AppStudentOnboardingCases_TenantId_AcademicYearId_DivisionI~",
                table: "AppStudentOnboardingCases");

            migrationBuilder.DropColumn(
                name: "DueAt",
                table: "AppStudentOnboardingCases");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentOnboardingPolicies_TenantId_AcademicYearId_Offeri~",
                table: "AppStudentOnboardingPolicies",
                columns: new[] { "TenantId", "AcademicYearId", "OfferingId", "StandardId" },
                unique: true);
        }
    }
}
