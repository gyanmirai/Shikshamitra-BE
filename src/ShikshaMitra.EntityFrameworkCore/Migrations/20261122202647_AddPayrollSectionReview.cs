using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollSectionReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ResolutionReason",
                table: "HR_PayrollSubmissions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ResolvedByUserId",
                table: "HR_PayrollSubmissions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewerRoleId",
                table: "HR_PayrollSubmissions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StageId",
                table: "HR_PayrollSubmissions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Mode",
                table: "HR_PayrollApprovalSettings",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(12)",
                oldMaxLength: 12);

            migrationBuilder.AddColumn<string>(
                name: "SectionRoutesJson",
                table: "HR_PayrollApprovalSettings",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_HR_PayrollSubmissions_TenantId_Month_ReviewerRoleId_Submitt~",
                table: "HR_PayrollSubmissions",
                columns: new[] { "TenantId", "Month", "ReviewerRoleId", "SubmittedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_HR_PayrollSubmissions_TenantId_Month_ReviewerRoleId_Submitt~",
                table: "HR_PayrollSubmissions");

            migrationBuilder.DropColumn(
                name: "ResolutionReason",
                table: "HR_PayrollSubmissions");

            migrationBuilder.DropColumn(
                name: "ResolvedByUserId",
                table: "HR_PayrollSubmissions");

            migrationBuilder.DropColumn(
                name: "ReviewerRoleId",
                table: "HR_PayrollSubmissions");

            migrationBuilder.DropColumn(
                name: "StageId",
                table: "HR_PayrollSubmissions");

            migrationBuilder.DropColumn(
                name: "SectionRoutesJson",
                table: "HR_PayrollApprovalSettings");

            migrationBuilder.AlterColumn<string>(
                name: "Mode",
                table: "HR_PayrollApprovalSettings",
                type: "character varying(12)",
                maxLength: 12,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16);
        }
    }
}
