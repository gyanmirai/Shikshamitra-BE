using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class HrAttendanceAutomatedAndTakeAttendance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalCorrelationId",
                table: "HR_AttendanceImportBatches",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntegrationProvider",
                table: "HR_AttendanceImportBatches",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsBatchLocked",
                table: "HR_AttendanceImportBatches",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "RunFinishedAt",
                table: "HR_AttendanceImportBatches",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RunSource",
                table: "HR_AttendanceImportBatches",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "RunStartedAt",
                table: "HR_AttendanceImportBatches",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Remarks",
                table: "AppEmployeeAttendances",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppStaffAttendanceDayLocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    EducationMediumStageId = table.Column<Guid>(type: "uuid", nullable: false),
                    LockDate = table.Column<DateOnly>(type: "date", nullable: false),
                    LockedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LockedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppStaffAttendanceDayLocks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HR_AttendanceImportBatches_ExternalCorrelationId",
                table: "HR_AttendanceImportBatches",
                column: "ExternalCorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_HR_AttendanceImportBatches_RunSource",
                table: "HR_AttendanceImportBatches",
                column: "RunSource");

            migrationBuilder.CreateIndex(
                name: "IX_StaffAttendanceDayLocks_EducationMediumStageId",
                table: "AppStaffAttendanceDayLocks",
                column: "EducationMediumStageId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffAttendanceDayLocks_Tenant_Stage_Date",
                table: "AppStaffAttendanceDayLocks",
                columns: new[] { "TenantId", "EducationMediumStageId", "LockDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppStaffAttendanceDayLocks");

            migrationBuilder.DropIndex(
                name: "IX_HR_AttendanceImportBatches_ExternalCorrelationId",
                table: "HR_AttendanceImportBatches");

            migrationBuilder.DropIndex(
                name: "IX_HR_AttendanceImportBatches_RunSource",
                table: "HR_AttendanceImportBatches");

            migrationBuilder.DropColumn(
                name: "ExternalCorrelationId",
                table: "HR_AttendanceImportBatches");

            migrationBuilder.DropColumn(
                name: "IntegrationProvider",
                table: "HR_AttendanceImportBatches");

            migrationBuilder.DropColumn(
                name: "IsBatchLocked",
                table: "HR_AttendanceImportBatches");

            migrationBuilder.DropColumn(
                name: "RunFinishedAt",
                table: "HR_AttendanceImportBatches");

            migrationBuilder.DropColumn(
                name: "RunSource",
                table: "HR_AttendanceImportBatches");

            migrationBuilder.DropColumn(
                name: "RunStartedAt",
                table: "HR_AttendanceImportBatches");

            migrationBuilder.DropColumn(
                name: "Remarks",
                table: "AppEmployeeAttendances");
        }
    }
}
