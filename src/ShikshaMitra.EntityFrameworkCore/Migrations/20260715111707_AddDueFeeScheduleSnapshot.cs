using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddDueFeeScheduleSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DueFees_TenantId_StudentId_FeeHeadId_Month_Year",
                table: "Finance_DueFees");

            migrationBuilder.AlterColumn<string>(
                name: "InstallmentLabel",
                table: "Finance_FeeCollectionDueFeeAllocations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FeeHeadName",
                table: "Finance_FeeCollectionDueFeeAllocations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CoverageEndDate",
                table: "Finance_DueFees",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CoverageStartDate",
                table: "Finance_DueFees",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DemandDate",
                table: "Finance_DueFees",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FeeStructureDetailId",
                table: "Finance_DueFees",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InstallmentName",
                table: "Finance_DueFees",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InstallmentSequence",
                table: "Finance_DueFees",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PaymentTermId",
                table: "Finance_DueFees",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PaymentTermInstallmentId",
                table: "Finance_DueFees",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PaymentTermVersionId",
                table: "Finance_DueFees",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduleDueDate",
                table: "Finance_DueFees",
                type: "timestamp with time zone",
                nullable: true);

            // Temporary nullable so we can backfill before enforcing uniqueness.
            migrationBuilder.AddColumn<string>(
                name: "ScheduleInstanceKey",
                table: "Finance_DueFees",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            // Default Legacy (1) so existing monthly dues stay on the legacy uniqueness path.
            migrationBuilder.AddColumn<int>(
                name: "ScheduleSource",
                table: "Finance_DueFees",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.Sql(
                """
                UPDATE "Finance_DueFees"
                SET
                    "ScheduleSource" = 1,
                    "ScheduleInstanceKey" =
                        REPLACE(LOWER("FeeHeadId"::text), '-', '')
                        || ':'
                        || "Year"::text
                        || ':'
                        || "Month"::text
                WHERE "ScheduleInstanceKey" IS NULL OR "ScheduleInstanceKey" = '';
                """);

            migrationBuilder.AlterColumn<string>(
                name: "ScheduleInstanceKey",
                table: "Finance_DueFees",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DueFees_TenantId_StudentId_FeeHeadId_Month_Year_Legacy",
                table: "Finance_DueFees",
                columns: new[] { "TenantId", "StudentId", "FeeHeadId", "Month", "Year" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"ScheduleSource\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_DueFees_TenantId_StudentId_ScheduleInstanceKey",
                table: "Finance_DueFees",
                columns: new[] { "TenantId", "StudentId", "ScheduleInstanceKey" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DueFees_TenantId_StudentId_FeeHeadId_Month_Year_Legacy",
                table: "Finance_DueFees");

            migrationBuilder.DropIndex(
                name: "IX_DueFees_TenantId_StudentId_ScheduleInstanceKey",
                table: "Finance_DueFees");

            migrationBuilder.DropColumn(
                name: "CoverageEndDate",
                table: "Finance_DueFees");

            migrationBuilder.DropColumn(
                name: "CoverageStartDate",
                table: "Finance_DueFees");

            migrationBuilder.DropColumn(
                name: "DemandDate",
                table: "Finance_DueFees");

            migrationBuilder.DropColumn(
                name: "FeeStructureDetailId",
                table: "Finance_DueFees");

            migrationBuilder.DropColumn(
                name: "InstallmentName",
                table: "Finance_DueFees");

            migrationBuilder.DropColumn(
                name: "InstallmentSequence",
                table: "Finance_DueFees");

            migrationBuilder.DropColumn(
                name: "PaymentTermId",
                table: "Finance_DueFees");

            migrationBuilder.DropColumn(
                name: "PaymentTermInstallmentId",
                table: "Finance_DueFees");

            migrationBuilder.DropColumn(
                name: "PaymentTermVersionId",
                table: "Finance_DueFees");

            migrationBuilder.DropColumn(
                name: "ScheduleDueDate",
                table: "Finance_DueFees");

            migrationBuilder.DropColumn(
                name: "ScheduleInstanceKey",
                table: "Finance_DueFees");

            migrationBuilder.DropColumn(
                name: "ScheduleSource",
                table: "Finance_DueFees");

            migrationBuilder.AlterColumn<string>(
                name: "InstallmentLabel",
                table: "Finance_FeeCollectionDueFeeAllocations",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FeeHeadName",
                table: "Finance_FeeCollectionDueFeeAllocations",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DueFees_TenantId_StudentId_FeeHeadId_Month_Year",
                table: "Finance_DueFees",
                columns: new[] { "TenantId", "StudentId", "FeeHeadId", "Month", "Year" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }
    }
}
