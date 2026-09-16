using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations;

/// <summary>
/// Removes the duplicated module word from prefixed academic table names.
/// This remains a rename-only migration so databases that already applied the
/// original prefix migration keep their data, storage objects and FK state.
/// </summary>
public partial class NormalizeAcademicTableNames : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        foreach (var (table, oldName, newName) in Constraints)
            RenameConstraint(migrationBuilder, table, oldName, newName);

        foreach (var (oldName, newName) in Tables)
            migrationBuilder.RenameTable(name: oldName, newName: newName);

        foreach (var (table, oldName, newName) in Indexes)
            migrationBuilder.RenameIndex(name: oldName, table: table, newName: newName);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (var (table, oldName, newName) in Indexes)
            migrationBuilder.RenameIndex(name: newName, table: table, newName: oldName);

        foreach (var (oldName, newName) in Tables)
            migrationBuilder.RenameTable(name: newName, newName: oldName);

        foreach (var (table, oldName, newName) in Constraints)
            RenameConstraint(migrationBuilder, table, newName, oldName);
    }

    private static void RenameConstraint(MigrationBuilder builder, string table, string from, string to)
    {
        // Preserve historical databases where optional model-declared FKs are absent.
        // Primary keys remain strict so unexpected schema damage stops deployment.
        if (from.StartsWith("FK_", System.StringComparison.Ordinal))
        {
            builder.Sql($$"""
                DO $academic_name_normalization$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM pg_constraint
                        WHERE conrelid = '"{{table}}"'::regclass AND conname = '{{from}}'
                    ) THEN
                        ALTER TABLE "{{table}}" RENAME CONSTRAINT "{{from}}" TO "{{to}}";
                    END IF;
                END $academic_name_normalization$;
                """);
        }
        else
        {
            builder.Sql($"ALTER TABLE \"{table}\" RENAME CONSTRAINT \"{from}\" TO \"{to}\";");
        }
    }

    private static readonly (string OldName, string NewName)[] Tables =
    [
        ("academic_AcademicCalendarAuditLogs", "academic_CalendarAuditLogs"),
        ("academic_AcademicCalendarAuditReviewNotes", "academic_CalendarAuditReviewNotes"),
        ("academic_AcademicCalendarDays", "academic_CalendarDays"),
        ("academic_AcademicCalendars", "academic_Calendars"),
        ("academic_AcademicCalendarSettings", "academic_CalendarSettings"),
        ("academic_AcademicCalendarWorkingHours", "academic_CalendarWorkingHours"),
        ("academic_AcademicSubstitutionAuditLogs", "academic_SubstitutionAuditLogs"),
        ("academic_AcademicSubstitutionNotificationLogs", "academic_SubstitutionNotificationLogs"),
        ("academic_AcademicSubstitutionRequests", "academic_SubstitutionRequests"),
        ("academic_AcademicTeacherAvailabilities", "academic_TeacherAvailabilities"),
        ("academic_AcademicYearPermissions", "academic_YearPermissions"),
        ("academic_AcademicYears", "academic_Years")
    ];

    private static readonly (string Table, string OldName, string NewName)[] Indexes =
    [
        ("academic_Years", "IX_academic_AcademicYears_Name_TenantId_StartDate_EndDate", "IX_academic_Years_Name_TenantId_StartDate_EndDate"),
        ("academic_TeacherAvailabilities", "IX_academic_AcademicTeacherAvailabilities_TeacherName_Availabi~", "IX_academic_TeacherAvailabilities_TeacherName_AvailabilityDate"),
        ("academic_TeacherAvailabilities", "IX_academic_AcademicTeacherAvailabilities_AcademicYearId_Teach~", "IX_academic_TeacherAvailabilities_AcademicYearId_TeacherId_Ava~"),
        ("academic_SubstitutionRequests", "IX_academic_AcademicSubstitutionRequests_SubstituteTeacherId_D~", "IX_academic_SubstitutionRequests_SubstituteTeacherId_Date_Star~"),
        ("academic_SubstitutionRequests", "IX_academic_AcademicSubstitutionRequests_Date_TimetableSlotId_~", "IX_academic_SubstitutionRequests_Date_TimetableSlotId_Timetabl~"),
        ("academic_SubstitutionRequests", "IX_academic_AcademicSubstitutionRequests_AcademicYearId_Date_S~", "IX_academic_SubstitutionRequests_AcademicYearId_Date_Status"),
        ("academic_SubstitutionNotificationLogs", "IX_academic_AcademicSubstitutionNotificationLogs_SubstitutionR~", "IX_academic_SubstitutionNotificationLogs_SubstitutionRequestId~"),
        ("academic_SubstitutionAuditLogs", "IX_academic_AcademicSubstitutionAuditLogs_SubstitutionRequestI~", "IX_academic_SubstitutionAuditLogs_SubstitutionRequestId_Timest~"),
        ("academic_CalendarWorkingHours", "IX_academic_AcademicCalendarWorkingHours_AcademicCalendarId_St~", "IX_academic_CalendarWorkingHours_AcademicCalendarId_StandardId~"),
        ("academic_CalendarSettings", "IX_academic_AcademicCalendarSettings_AcademicMediumId", "IX_academic_CalendarSettings_AcademicMediumId"),
        ("academic_Calendars", "IX_academic_AcademicCalendars_AcademicMediumId_IsDefault", "IX_academic_Calendars_AcademicMediumId_IsDefault"),
        ("academic_CalendarDays", "IX_academic_AcademicCalendarDays_Date_DayType", "IX_academic_CalendarDays_Date_DayType"),
        ("academic_CalendarDays", "IX_academic_AcademicCalendarDays_AcademicCalendarId_Date", "IX_academic_CalendarDays_AcademicCalendarId_Date"),
        ("academic_CalendarAuditReviewNotes", "IX_academic_AcademicCalendarAuditReviewNotes_TenantId_AuditLog~", "IX_academic_CalendarAuditReviewNotes_TenantId_AuditLogId_Creat~"),
        ("academic_CalendarAuditLogs", "IX_academic_AcademicCalendarAuditLogs_AcademicCalendarId", "IX_academic_CalendarAuditLogs_AcademicCalendarId")
    ];

    private static readonly (string Table, string OldName, string NewName)[] Constraints =
    [
        ("academic_AcademicCalendarDays", "FK_academic_AcademicCalendarDays_academic_AcademicCalendars_Ac~", "FK_academic_CalendarDays_academic_Calendars_AcademicCalendarId"),
        ("academic_EducationBoards", "FK_academic_EducationBoards_academic_AcademicYears_AcademicYea~", "FK_academic_EducationBoards_academic_Years_AcademicYearId"),
        ("AppAcademicTerms", "FK_AppAcademicTerms_academic_AcademicYears_AcademicYearId", "FK_AppAcademicTerms_academic_Years_AcademicYearId"),
        ("AppEmployeeLeavePolicies", "FK_AppEmployeeLeavePolicies_academic_AcademicYears_AcademicYea~", "FK_AppEmployeeLeavePolicies_academic_Years_AcademicYearId"),
        ("AppEvents", "FK_AppEvents_academic_AcademicYears_AcademicYearId", "FK_AppEvents_academic_Years_AcademicYearId"),
        ("AppGroups", "FK_AppGroups_academic_AcademicYears_AcademicYearId", "FK_AppGroups_academic_Years_AcademicYearId"),
        ("AppHolidays", "FK_AppHolidays_academic_AcademicYears_AcademicYearId", "FK_AppHolidays_academic_Years_AcademicYearId"),
        ("AppStudentOnboardingCases", "FK_AppStudentOnboardingCases_academic_AcademicYears_AcademicYe~", "FK_AppStudentOnboardingCases_academic_Years_AcademicYearId"),
        ("AppStudents", "FK_AppStudents_academic_AcademicYears_AcademicYearId", "FK_AppStudents_academic_Years_AcademicYearId"),
        ("Finance_FeeStructureMetadatas", "FK_Finance_FeeStructureMetadatas_academic_AcademicYears_Academ~", "FK_Finance_FeeStructureMetadatas_academic_Years_AcademicYearId"),
        ("Finance_ReceiptBooks", "FK_Finance_ReceiptBooks_academic_AcademicYears_AcademicYearId", "FK_Finance_ReceiptBooks_academic_Years_AcademicYearId"),
        ("academic_AcademicYears", "PK_academic_AcademicYears", "PK_academic_Years"),
        ("academic_AcademicYearPermissions", "PK_academic_AcademicYearPermissions", "PK_academic_YearPermissions"),
        ("academic_AcademicTeacherAvailabilities", "PK_academic_AcademicTeacherAvailabilities", "PK_academic_TeacherAvailabilities"),
        ("academic_AcademicSubstitutionRequests", "PK_academic_AcademicSubstitutionRequests", "PK_academic_SubstitutionRequests"),
        ("academic_AcademicSubstitutionNotificationLogs", "PK_academic_AcademicSubstitutionNotificationLogs", "PK_academic_SubstitutionNotificationLogs"),
        ("academic_AcademicSubstitutionAuditLogs", "PK_academic_AcademicSubstitutionAuditLogs", "PK_academic_SubstitutionAuditLogs"),
        ("academic_AcademicCalendarWorkingHours", "PK_academic_AcademicCalendarWorkingHours", "PK_academic_CalendarWorkingHours"),
        ("academic_AcademicCalendarSettings", "PK_academic_AcademicCalendarSettings", "PK_academic_CalendarSettings"),
        ("academic_AcademicCalendars", "PK_academic_AcademicCalendars", "PK_academic_Calendars"),
        ("academic_AcademicCalendarDays", "PK_academic_AcademicCalendarDays", "PK_academic_CalendarDays"),
        ("academic_AcademicCalendarAuditReviewNotes", "PK_academic_AcademicCalendarAuditReviewNotes", "PK_academic_CalendarAuditReviewNotes"),
        ("academic_AcademicCalendarAuditLogs", "PK_academic_AcademicCalendarAuditLogs", "PK_academic_CalendarAuditLogs")
    ];
}
