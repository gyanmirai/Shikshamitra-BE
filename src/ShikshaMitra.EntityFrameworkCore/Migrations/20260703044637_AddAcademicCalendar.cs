using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddAcademicCalendar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppAcademicCalendarAuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicCalendarId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcademicCalendarDayId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    OldValueJson = table.Column<string>(type: "text", nullable: false),
                    NewValueJson = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    ChangedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_AppAcademicCalendarAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppAcademicCalendars",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    PublishedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_AppAcademicCalendars", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppAcademicCalendarSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    WeekendRule = table.Column<int>(type: "integer", nullable: false),
                    AllowClassSpecificHolidays = table.Column<bool>(type: "boolean", nullable: false),
                    AllowDivisionSpecificHolidays = table.Column<bool>(type: "boolean", nullable: false),
                    EnableHolidayCategories = table.Column<bool>(type: "boolean", nullable: false),
                    EnableVacationRanges = table.Column<bool>(type: "boolean", nullable: false),
                    EnableHalfDayRules = table.Column<bool>(type: "boolean", nullable: false),
                    BlockTimetableOnHoliday = table.Column<bool>(type: "boolean", nullable: false),
                    BlockTimetableOnVacation = table.Column<bool>(type: "boolean", nullable: false),
                    BlockTimetableOnEmergencyClosure = table.Column<bool>(type: "boolean", nullable: false),
                    BlockHomeworkDueDateOnHoliday = table.Column<bool>(type: "boolean", nullable: false),
                    BlockHomeworkDueDateOnVacation = table.Column<bool>(type: "boolean", nullable: false),
                    BlockExamScheduleOnHoliday = table.Column<bool>(type: "boolean", nullable: false),
                    BlockExamScheduleOnVacation = table.Column<bool>(type: "boolean", nullable: false),
                    AllowExamOnHalfDay = table.Column<bool>(type: "boolean", nullable: false),
                    BlockLessonPlanOnHoliday = table.Column<bool>(type: "boolean", nullable: false),
                    BlockLessonPlanOnVacation = table.Column<bool>(type: "boolean", nullable: false),
                    BlockOnlineClassOnHoliday = table.Column<bool>(type: "boolean", nullable: false),
                    BlockOnlineClassOnEventDay = table.Column<bool>(type: "boolean", nullable: false),
                    AutoExemptAttendanceOnHoliday = table.Column<bool>(type: "boolean", nullable: false),
                    AutoExemptAttendanceOnVacation = table.Column<bool>(type: "boolean", nullable: false),
                    EnableEmergencyClosure = table.Column<bool>(type: "boolean", nullable: false),
                    EmergencyClosureRequiresPrincipalApproval = table.Column<bool>(type: "boolean", nullable: false),
                    AutoScanImpactedActivities = table.Column<bool>(type: "boolean", nullable: false),
                    RescheduleMode = table.Column<int>(type: "integer", nullable: false),
                    NotifyTeachersOnCalendarChange = table.Column<bool>(type: "boolean", nullable: false),
                    NotifyParentsOnCalendarChange = table.Column<bool>(type: "boolean", nullable: false),
                    NotifyStudentsOnCalendarChange = table.Column<bool>(type: "boolean", nullable: false),
                    NotifyStaffOnCalendarChange = table.Column<bool>(type: "boolean", nullable: false),
                    CalendarVisibility = table.Column<int>(type: "integer", nullable: false),
                    RequireReasonForPublishedCalendarChange = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_AppAcademicCalendarSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppAcademicCalendarWorkingHours",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicCalendarId = table.Column<Guid>(type: "uuid", nullable: false),
                    StandardId = table.Column<Guid>(type: "uuid", nullable: true),
                    DivisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ShiftType = table.Column<int>(type: "integer", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    EndTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    RecessStartTime = table.Column<TimeSpan>(type: "interval", nullable: true),
                    RecessEndTime = table.Column<TimeSpan>(type: "interval", nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_AppAcademicCalendarWorkingHours", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppCalendarImpactLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicCalendarDayId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImpactType = table.Column<int>(type: "integer", nullable: false),
                    AffectedEntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    AffectedEntityName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    AffectedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Message = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SuggestedNewDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsResolved = table.Column<bool>(type: "boolean", nullable: false),
                    ResolvedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolutionNote = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
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
                    table.PrimaryKey("PK_AppCalendarImpactLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppAcademicCalendarDays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicCalendarId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DayType = table.Column<int>(type: "integer", nullable: false),
                    HolidayCategory = table.Column<int>(type: "integer", nullable: true),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    AppliesToAll = table.Column<bool>(type: "boolean", nullable: false),
                    StandardId = table.Column<Guid>(type: "uuid", nullable: true),
                    DivisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    StartTime = table.Column<TimeSpan>(type: "interval", nullable: true),
                    EndTime = table.Column<TimeSpan>(type: "interval", nullable: true),
                    BlocksTimetable = table.Column<bool>(type: "boolean", nullable: false),
                    BlocksHomeworkDueDate = table.Column<bool>(type: "boolean", nullable: false),
                    BlocksExamSchedule = table.Column<bool>(type: "boolean", nullable: false),
                    BlocksLessonPlan = table.Column<bool>(type: "boolean", nullable: false),
                    BlocksOnlineClass = table.Column<bool>(type: "boolean", nullable: false),
                    ExemptAttendance = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ChangeReason = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
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
                    table.PrimaryKey("PK_AppAcademicCalendarDays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppAcademicCalendarDays_AppAcademicCalendars_AcademicCalend~",
                        column: x => x.AcademicCalendarId,
                        principalTable: "AppAcademicCalendars",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppAcademicCalendarAuditLogs_AcademicCalendarId",
                table: "AppAcademicCalendarAuditLogs",
                column: "AcademicCalendarId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAcademicCalendarDays_AcademicCalendarId_Date",
                table: "AppAcademicCalendarDays",
                columns: new[] { "AcademicCalendarId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppAcademicCalendarDays_Date_DayType",
                table: "AppAcademicCalendarDays",
                columns: new[] { "Date", "DayType" });

            migrationBuilder.CreateIndex(
                name: "IX_AppAcademicCalendars_AcademicYearId_IsDefault",
                table: "AppAcademicCalendars",
                columns: new[] { "AcademicYearId", "IsDefault" });

            migrationBuilder.CreateIndex(
                name: "IX_AppAcademicCalendarSettings_AcademicYearId",
                table: "AppAcademicCalendarSettings",
                column: "AcademicYearId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppAcademicCalendarWorkingHours_AcademicCalendarId_Standard~",
                table: "AppAcademicCalendarWorkingHours",
                columns: new[] { "AcademicCalendarId", "StandardId", "DivisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppCalendarImpactLogs_AcademicCalendarDayId_Status",
                table: "AppCalendarImpactLogs",
                columns: new[] { "AcademicCalendarDayId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppAcademicCalendarAuditLogs");

            migrationBuilder.DropTable(
                name: "AppAcademicCalendarDays");

            migrationBuilder.DropTable(
                name: "AppAcademicCalendarSettings");

            migrationBuilder.DropTable(
                name: "AppAcademicCalendarWorkingHours");

            migrationBuilder.DropTable(
                name: "AppCalendarImpactLogs");

            migrationBuilder.DropTable(
                name: "AppAcademicCalendars");
        }
    }
}
