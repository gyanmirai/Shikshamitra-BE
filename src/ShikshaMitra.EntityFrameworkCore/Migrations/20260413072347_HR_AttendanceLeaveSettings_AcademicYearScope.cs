using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class HR_AttendanceLeaveSettings_AcademicYearScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Hard reset requested for legacy leave-settings and attendance-rule data.
            migrationBuilder.Sql("DELETE FROM \"AppEmployeeLeaves\";");
            migrationBuilder.Sql("DELETE FROM \"AppEmployeeLeavePolicies\";");
            migrationBuilder.Sql("DELETE FROM \"AppEmployeeLeaveTypes\";");

            migrationBuilder.DropForeignKey(
                name: "FK_AppEmployeeLeavePolicies_AppEducationMediumStages_Education~",
                table: "AppEmployeeLeavePolicies");

            migrationBuilder.DropTable(
                name: "AppCustomAttendanceRules");

            migrationBuilder.RenameColumn(
                name: "EducationMediumStageId",
                table: "AppEmployeeLeaveTypes",
                newName: "AcademicYearId");

            migrationBuilder.RenameIndex(
                name: "IX_EmployeeLeaveTypes_TenantId_MediumStageId_Type",
                table: "AppEmployeeLeaveTypes",
                newName: "IX_EmployeeLeaveTypes_TenantId_AcademicYearId_Type");

            migrationBuilder.RenameIndex(
                name: "IX_EmployeeLeaveTypes_TenantId_MediumStageId_IsActive",
                table: "AppEmployeeLeaveTypes",
                newName: "IX_EmployeeLeaveTypes_TenantId_AcademicYearId_IsActive");

            migrationBuilder.RenameColumn(
                name: "MaximumDaysPerYear",
                table: "AppEmployeeLeavePolicies",
                newName: "LeaveCount");

            migrationBuilder.RenameColumn(
                name: "EducationMediumStageId",
                table: "AppEmployeeLeavePolicies",
                newName: "AcademicYearId");

            migrationBuilder.RenameIndex(
                name: "IX_EmployeeLeavePolicies_TenantId_MediumStageId_PolicyName",
                table: "AppEmployeeLeavePolicies",
                newName: "IX_EmployeeLeavePolicies_TenantId_AcademicYearId_PolicyName");

            migrationBuilder.RenameIndex(
                name: "IX_EmployeeLeavePolicies_TenantId_MediumStageId_IsActive",
                table: "AppEmployeeLeavePolicies",
                newName: "IX_EmployeeLeavePolicies_TenantId_AcademicYearId_IsActive");

            migrationBuilder.RenameIndex(
                name: "IX_AppEmployeeLeavePolicies_EducationMediumStageId",
                table: "AppEmployeeLeavePolicies",
                newName: "IX_AppEmployeeLeavePolicies_AcademicYearId");

            migrationBuilder.AddColumn<int>(
                name: "AccrualPolicyType",
                table: "AppEmployeeLeavePolicies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EncashmentTiming",
                table: "AppEmployeeLeavePolicies",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppAttendanceConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    HoursBasedPayPercentage = table.Column<double>(type: "double precision", nullable: false),
                    GraceTime = table.Column<int>(type: "integer", nullable: false),
                    MaxLateAllowedPerMonth = table.Column<int>(type: "integer", nullable: false),
                    PenaltyType = table.Column<int>(type: "integer", nullable: false),
                    OvertimeEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    OvertimeRate = table.Column<int>(type: "integer", nullable: true),
                    MinOvertimeMinutes = table.Column<int>(type: "integer", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppAttendanceConfigurations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppEmployeeLeaveTypes_AcademicYearId",
                table: "AppEmployeeLeaveTypes",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceConfigurations_TenantId_AcademicYearId",
                table: "AppAttendanceConfigurations",
                columns: new[] { "TenantId", "AcademicYearId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AppEmployeeLeavePolicies_AppAcademicYears_AcademicYearId",
                table: "AppEmployeeLeavePolicies",
                column: "AcademicYearId",
                principalTable: "AppAcademicYears",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AppEmployeeLeaveTypes_AppAcademicYears_AcademicYearId",
                table: "AppEmployeeLeaveTypes",
                column: "AcademicYearId",
                principalTable: "AppAcademicYears",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppEmployeeLeavePolicies_AppAcademicYears_AcademicYearId",
                table: "AppEmployeeLeavePolicies");

            migrationBuilder.DropForeignKey(
                name: "FK_AppEmployeeLeaveTypes_AppAcademicYears_AcademicYearId",
                table: "AppEmployeeLeaveTypes");

            migrationBuilder.DropTable(
                name: "AppAttendanceConfigurations");

            migrationBuilder.DropIndex(
                name: "IX_AppEmployeeLeaveTypes_AcademicYearId",
                table: "AppEmployeeLeaveTypes");

            migrationBuilder.DropColumn(
                name: "AccrualPolicyType",
                table: "AppEmployeeLeavePolicies");

            migrationBuilder.DropColumn(
                name: "EncashmentTiming",
                table: "AppEmployeeLeavePolicies");

            migrationBuilder.RenameColumn(
                name: "AcademicYearId",
                table: "AppEmployeeLeaveTypes",
                newName: "EducationMediumStageId");

            migrationBuilder.RenameIndex(
                name: "IX_EmployeeLeaveTypes_TenantId_AcademicYearId_Type",
                table: "AppEmployeeLeaveTypes",
                newName: "IX_EmployeeLeaveTypes_TenantId_MediumStageId_Type");

            migrationBuilder.RenameIndex(
                name: "IX_EmployeeLeaveTypes_TenantId_AcademicYearId_IsActive",
                table: "AppEmployeeLeaveTypes",
                newName: "IX_EmployeeLeaveTypes_TenantId_MediumStageId_IsActive");

            migrationBuilder.RenameColumn(
                name: "LeaveCount",
                table: "AppEmployeeLeavePolicies",
                newName: "MaximumDaysPerYear");

            migrationBuilder.RenameColumn(
                name: "AcademicYearId",
                table: "AppEmployeeLeavePolicies",
                newName: "EducationMediumStageId");

            migrationBuilder.RenameIndex(
                name: "IX_EmployeeLeavePolicies_TenantId_AcademicYearId_PolicyName",
                table: "AppEmployeeLeavePolicies",
                newName: "IX_EmployeeLeavePolicies_TenantId_MediumStageId_PolicyName");

            migrationBuilder.RenameIndex(
                name: "IX_EmployeeLeavePolicies_TenantId_AcademicYearId_IsActive",
                table: "AppEmployeeLeavePolicies",
                newName: "IX_EmployeeLeavePolicies_TenantId_MediumStageId_IsActive");

            migrationBuilder.RenameIndex(
                name: "IX_AppEmployeeLeavePolicies_AcademicYearId",
                table: "AppEmployeeLeavePolicies",
                newName: "IX_AppEmployeeLeavePolicies_EducationMediumStageId");

            migrationBuilder.CreateTable(
                name: "AppCustomAttendanceRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    EnableNotificationForUpperAuthority = table.Column<bool>(type: "boolean", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    HalfDayThreshold = table.Column<int>(type: "integer", nullable: false),
                    HalfDayThresholdUnit = table.Column<int>(type: "integer", nullable: false),
                    LateThreshold = table.Column<int>(type: "integer", nullable: false),
                    LateThresholdUnit = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppCustomAttendanceRules", x => x.Id);
                });

            migrationBuilder.AddForeignKey(
                name: "FK_AppEmployeeLeavePolicies_AppEducationMediumStages_Education~",
                table: "AppEmployeeLeavePolicies",
                column: "EducationMediumStageId",
                principalTable: "AppEducationMediumStages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
