using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class SalaryTemplateActivationFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DefaultPfApplicable",
                table: "HR_SalaryStructureTemplateVersions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultPfRuleId",
                table: "HR_SalaryStructureTemplateVersions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SaveOperationKey",
                table: "HR_SalaryStructureTemplateVersions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CustomizationReason",
                table: "HR_EmployeeCompensationRevisions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "HR_EmployeeCompensationRevisions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OverridesJson",
                table: "HR_EmployeeCompensationRevisions",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.CreateIndex(
                name: "IX_HR_SalaryStructureTemplateVersions_TenantId_SaveOperationKey",
                table: "HR_SalaryStructureTemplateVersions",
                columns: new[] { "TenantId", "SaveOperationKey" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"SaveOperationKey\" <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_HR_EmployeeCompensationRevisions_TenantId_EmployeeId_Idempo~",
                table: "HR_EmployeeCompensationRevisions",
                columns: new[] { "TenantId", "EmployeeId", "IdempotencyKey" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"IdempotencyKey\" <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_HR_SalaryStructureTemplateVersions_TenantId_SaveOperationKey",
                table: "HR_SalaryStructureTemplateVersions");

            migrationBuilder.DropIndex(
                name: "IX_HR_EmployeeCompensationRevisions_TenantId_EmployeeId_Idempo~",
                table: "HR_EmployeeCompensationRevisions");

            migrationBuilder.DropColumn(
                name: "DefaultPfApplicable",
                table: "HR_SalaryStructureTemplateVersions");

            migrationBuilder.DropColumn(
                name: "DefaultPfRuleId",
                table: "HR_SalaryStructureTemplateVersions");

            migrationBuilder.DropColumn(
                name: "SaveOperationKey",
                table: "HR_SalaryStructureTemplateVersions");

            migrationBuilder.DropColumn(
                name: "CustomizationReason",
                table: "HR_EmployeeCompensationRevisions");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "HR_EmployeeCompensationRevisions");

            migrationBuilder.DropColumn(
                name: "OverridesJson",
                table: "HR_EmployeeCompensationRevisions");
        }
    }
}
