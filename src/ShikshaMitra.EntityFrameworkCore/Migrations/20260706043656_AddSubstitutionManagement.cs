using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddSubstitutionManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppAcademicSubstitutionAuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubstitutionRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Action = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Details = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppAcademicSubstitutionAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppAcademicSubstitutionRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: true),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Day = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Period = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    StandardId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClassName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DivisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    DivisionName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TimetableId = table.Column<Guid>(type: "uuid", nullable: true),
                    TimetableSlotId = table.Column<Guid>(type: "uuid", nullable: true),
                    TimetableSlotSubjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubjectName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DepartmentName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    OriginalTeacherId = table.Column<Guid>(type: "uuid", nullable: true),
                    OriginalTeacherName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SubstituteTeacherId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubstituteTeacherName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    OriginalRoomId = table.Column<Guid>(type: "uuid", nullable: true),
                    RoomName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    AssignedRoomId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignedRoomName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    AcknowledgementStatus = table.Column<int>(type: "integer", nullable: false),
                    NotificationStatus = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    ConflictStatus = table.Column<int>(type: "integer", nullable: false),
                    ConflictReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SuggestionScore = table.Column<int>(type: "integer", nullable: true),
                    WorkloadStatus = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AcknowledgementRequired = table.Column<bool>(type: "boolean", nullable: false),
                    AcknowledgedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Remarks = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_AppAcademicSubstitutionRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppAcademicTeacherAvailabilities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: true),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: true),
                    TeacherName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DepartmentName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    AvailabilityDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    AvailabilityStatus = table.Column<int>(type: "integer", nullable: false),
                    SourceType = table.Column<int>(type: "integer", nullable: false),
                    SourceEntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    IsFullDay = table.Column<bool>(type: "boolean", nullable: false),
                    AffectedLectureCount = table.Column<int>(type: "integer", nullable: false),
                    AutoCreateSubstitutions = table.Column<bool>(type: "boolean", nullable: false),
                    NotifyLeadership = table.Column<bool>(type: "boolean", nullable: false),
                    AttachmentName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Remarks = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsCancelled = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_AppAcademicTeacherAvailabilities", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppAcademicSubstitutionAuditLogs_SubstitutionRequestId_Time~",
                table: "AppAcademicSubstitutionAuditLogs",
                columns: new[] { "SubstitutionRequestId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_AppAcademicSubstitutionRequests_AcademicYearId_Date_Status",
                table: "AppAcademicSubstitutionRequests",
                columns: new[] { "AcademicYearId", "Date", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AppAcademicSubstitutionRequests_Date_TimetableSlotId_Timeta~",
                table: "AppAcademicSubstitutionRequests",
                columns: new[] { "Date", "TimetableSlotId", "TimetableSlotSubjectId", "OriginalTeacherId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppAcademicSubstitutionRequests_SubstituteTeacherId_Date_St~",
                table: "AppAcademicSubstitutionRequests",
                columns: new[] { "SubstituteTeacherId", "Date", "StartTime", "EndTime" });

            migrationBuilder.CreateIndex(
                name: "IX_AppAcademicTeacherAvailabilities_AcademicYearId_TeacherId_A~",
                table: "AppAcademicTeacherAvailabilities",
                columns: new[] { "AcademicYearId", "TeacherId", "AvailabilityDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AppAcademicTeacherAvailabilities_TeacherName_AvailabilityDa~",
                table: "AppAcademicTeacherAvailabilities",
                columns: new[] { "TeacherName", "AvailabilityDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppAcademicSubstitutionAuditLogs");

            migrationBuilder.DropTable(
                name: "AppAcademicSubstitutionRequests");

            migrationBuilder.DropTable(
                name: "AppAcademicTeacherAvailabilities");
        }
    }
}
