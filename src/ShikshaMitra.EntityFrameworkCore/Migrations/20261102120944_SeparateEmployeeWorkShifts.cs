using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class SeparateEmployeeWorkShifts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ValidateLegacyReferences(migrationBuilder);

            migrationBuilder.DropForeignKey(
                name: "FK_AppEmployees_academic_ShiftManagements_ShiftId",
                table: "AppEmployees");

            migrationBuilder.CreateTable(
                name: "HR_WorkShifts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShiftName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LegacySchoolShiftId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_HR_WorkShifts", x => x.Id);
                    table.CheckConstraint("CK_HR_WorkShifts_Daytime", "\"EndTime\" > \"StartTime\"");
                });

            migrationBuilder.CreateTable(
                name: "HR_EmployeeShiftAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkShiftId = table.Column<Guid>(type: "uuid", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    ShiftName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    Provenance = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_HR_EmployeeShiftAssignments", x => x.Id);
                    table.CheckConstraint("CK_HR_ShiftAssignment_Range", "\"EffectiveTo\" IS NULL OR \"EffectiveTo\" > \"EffectiveFrom\"");
                    table.ForeignKey(
                        name: "FK_HR_EmployeeShiftAssignments_AppEmployees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "AppEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HR_EmployeeShiftAssignments_HR_WorkShifts_WorkShiftId",
                        column: x => x.WorkShiftId,
                        principalTable: "HR_WorkShifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HR_EmployeeShiftAssignments_EmployeeId",
                table: "HR_EmployeeShiftAssignments",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_HR_EmployeeShiftAssignments_TenantId_EmployeeId_EffectiveFr~",
                table: "HR_EmployeeShiftAssignments",
                columns: new[] { "TenantId", "EmployeeId", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_HR_EmployeeShiftAssignments_WorkShiftId",
                table: "HR_EmployeeShiftAssignments",
                column: "WorkShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_HR_WorkShifts_TenantId_LegacySchoolShiftId",
                table: "HR_WorkShifts",
                columns: new[] { "TenantId", "LegacySchoolShiftId" },
                unique: true);

            PopulateIndependentWorkShifts(migrationBuilder);

            migrationBuilder.AddForeignKey(
                name: "FK_AppEmployees_HR_WorkShifts_ShiftId",
                table: "AppEmployees",
                column: "ShiftId",
                principalTable: "HR_WorkShifts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new InvalidOperationException("Restore the verified pre-cutover backup before reopening writes; after reopening use a reviewed forward repair.");
        }
    }
}
