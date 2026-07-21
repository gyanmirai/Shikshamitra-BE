using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    public partial class AddAdmissionCrmFunctionalConfiguration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EntranceExamRequired",
                table: "AppAdmissionCrmCycleConfigurations",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "JourneyLocked",
                table: "AppAdmissionCrmCycleConfigurations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "JourneyLockedAt",
                table: "AppAdmissionCrmCycleConfigurations",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "JourneyLockedByUserId",
                table: "AppAdmissionCrmCycleConfigurations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JourneyLockReason",
                table: "AppAdmissionCrmCycleConfigurations",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BaseSystemStage",
                table: "AppAdmissionCrmPipelineStages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "StageKey",
                table: "AppAdmissionCrmPipelineStages",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(@"
UPDATE ""AppAdmissionCrmPipelineStages""
SET ""StageKey"" = CASE ""SystemStage""
        WHEN 0 THEN 'NewInquiry'
        WHEN 1 THEN 'FollowUp'
        WHEN 2 THEN 'ExamReady'
        WHEN 3 THEN 'ExamScheduled'
        WHEN 4 THEN 'ExamCompleted'
        WHEN 5 THEN 'Eligible'
        WHEN 6 THEN 'Waitlisted'
        WHEN 7 THEN 'Rejected'
        WHEN 8 THEN 'Stale'
        WHEN 9 THEN 'Archived'
        ELSE ""SystemStage""::text
    END,
    ""BaseSystemStage"" = ""SystemStage""
WHERE ""StageKey"" = '';
");

            migrationBuilder.DropIndex(
                name: "IX_AppAdmissionCrmPipelineStages_TenantId_AcademicYearId_Syste~",
                table: "AppAdmissionCrmPipelineStages");

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionCrmPipelineStages_TenantId_AcademicYearId_StageKey",
                table: "AppAdmissionCrmPipelineStages",
                columns: new[] { "TenantId", "AcademicYearId", "StageKey" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionCrmPipelineStages_TenantId_AcademicYearId_SystemStage",
                table: "AppAdmissionCrmPipelineStages",
                columns: new[] { "TenantId", "AcademicYearId", "SystemStage" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppAdmissionCrmPipelineStages_TenantId_AcademicYearId_StageKey",
                table: "AppAdmissionCrmPipelineStages");

            migrationBuilder.DropIndex(
                name: "IX_AppAdmissionCrmPipelineStages_TenantId_AcademicYearId_SystemStage",
                table: "AppAdmissionCrmPipelineStages");

            migrationBuilder.DropColumn(
                name: "BaseSystemStage",
                table: "AppAdmissionCrmPipelineStages");

            migrationBuilder.DropColumn(
                name: "StageKey",
                table: "AppAdmissionCrmPipelineStages");

            migrationBuilder.DropColumn(
                name: "EntranceExamRequired",
                table: "AppAdmissionCrmCycleConfigurations");

            migrationBuilder.DropColumn(
                name: "JourneyLocked",
                table: "AppAdmissionCrmCycleConfigurations");

            migrationBuilder.DropColumn(
                name: "JourneyLockedAt",
                table: "AppAdmissionCrmCycleConfigurations");

            migrationBuilder.DropColumn(
                name: "JourneyLockedByUserId",
                table: "AppAdmissionCrmCycleConfigurations");

            migrationBuilder.DropColumn(
                name: "JourneyLockReason",
                table: "AppAdmissionCrmCycleConfigurations");

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionCrmPipelineStages_TenantId_AcademicYearId_Syste~",
                table: "AppAdmissionCrmPipelineStages",
                columns: new[] { "TenantId", "AcademicYearId", "SystemStage" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }
    }
}
