using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppAttendanceDays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    StandardId = table.Column<Guid>(type: "uuid", nullable: false),
                    DivisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateTime>(type: "date", nullable: false),
                    IsLocked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    MarkedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_AppAttendanceDays", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppStudentLeaveTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaveTypeName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_AppStudentLeaveTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppAttendanceEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AttendanceDayId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Remarks = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MarkedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_AppAttendanceEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppAttendanceEntries_AppAttendanceDays_AttendanceDayId",
                        column: x => x.AttendanceDayId,
                        principalTable: "AppAttendanceDays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppAttendanceEntries_AppStudents_StudentId",
                        column: x => x.StudentId,
                        principalTable: "AppStudents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppStudentLeaves",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    EducationMediumId = table.Column<Guid>(type: "uuid", nullable: true),
                    StandardId = table.Column<Guid>(type: "uuid", nullable: true),
                    DivisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentLeaveTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsHalfDay = table.Column<bool>(type: "boolean", nullable: false),
                    LeaveDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TotalDays = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    Remarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RejectionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Attachment = table.Column<byte[]>(type: "bytea", nullable: true),
                    AttachmentFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    AttachmentContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
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
                    table.PrimaryKey("PK_AppStudentLeaves", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppStudentLeaves_AppStudentLeaveTypes_StudentLeaveTypeId",
                        column: x => x.StudentLeaveTypeId,
                        principalTable: "AppStudentLeaveTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppStudentLeaves_AppStudents_StudentId",
                        column: x => x.StudentId,
                        principalTable: "AppStudents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UX_AttendanceDay_Unique",
                table: "AppAttendanceDays",
                columns: new[] { "AcademicYearId", "StandardId", "DivisionId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppAttendanceEntries_AttendanceDayId",
                table: "AppAttendanceEntries",
                column: "AttendanceDayId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceEntry_Student",
                table: "AppAttendanceEntries",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "UX_AttendanceEntry_Unique",
                table: "AppAttendanceEntries",
                columns: new[] { "AttendanceDayId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentLeaves_IsDeleted",
                table: "AppStudentLeaves",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentLeaves_LeaveDate",
                table: "AppStudentLeaves",
                column: "LeaveDate");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentLeaves_Status",
                table: "AppStudentLeaves",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentLeaves_StudentId",
                table: "AppStudentLeaves",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentLeaves_StudentLeaveTypeId",
                table: "AppStudentLeaves",
                column: "StudentLeaveTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentLeaves_TenantId",
                table: "AppStudentLeaves",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentLeaveTypes_IsActive",
                table: "AppStudentLeaveTypes",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentLeaveTypes_LeaveTypeName",
                table: "AppStudentLeaveTypes",
                column: "LeaveTypeName");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentLeaveTypes_TenantId_LeaveTypeName",
                table: "AppStudentLeaveTypes",
                columns: new[] { "TenantId", "LeaveTypeName" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppAttendanceEntries");

            migrationBuilder.DropTable(
                name: "AppStudentLeaves");

            migrationBuilder.DropTable(
                name: "AppAttendanceDays");

            migrationBuilder.DropTable(
                name: "AppStudentLeaveTypes");
        }
    }
}
