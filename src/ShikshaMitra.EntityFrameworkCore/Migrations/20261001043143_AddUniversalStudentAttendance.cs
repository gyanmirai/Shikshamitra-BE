using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddUniversalStudentAttendance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AttendancePlacementHistoryJson",
                table: "AppStudentEnrollments",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<DateTime>(
                name: "AttendanceStartDate",
                table: "AppStudentEnrollments",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedLeaveId",
                table: "AppAttendanceEntries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "DenominatorCredit",
                table: "AppAttendanceEntries",
                type: "double precision",
                nullable: false,
                defaultValue: 1.0);

            migrationBuilder.AddColumn<Guid>(
                name: "EnrollmentId",
                table: "AppAttendanceEntries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ExcludedFromPercentage",
                table: "AppAttendanceEntries",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ExpectedAtSubmission",
                table: "AppAttendanceEntries",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HalfDay",
                table: "AppAttendanceEntries",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "HistoricalRollNo",
                table: "AppAttendanceEntries",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Late",
                table: "AppAttendanceEntries",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PolicySnapshotJson",
                table: "AppAttendanceEntries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PresentCredit",
                table: "AppAttendanceEntries",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SchoolDate",
                table: "AppAttendanceEntries",
                type: "date",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "IsSubmitted",
                table: "AppAttendanceDays",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LastOperationHash",
                table: "AppAttendanceDays",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastOperationId",
                table: "AppAttendanceDays",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PolicySnapshotJson",
                table: "AppAttendanceDays",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Revision",
                table: "AppAttendanceDays",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SubmittedRosterRevision",
                table: "AppAttendanceDays",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppStudentAttendanceConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: true),
                    DraftPolicyJson = table.Column<string>(type: "text", nullable: false),
                    PublishedVersionNumber = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_AppStudentAttendanceConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppStudentAttendanceCorrections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AttendanceDayId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BeforeJson = table.Column<string>(type: "text", nullable: false),
                    RequestJson = table.Column<string>(type: "text", nullable: false),
                    PolicyJson = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ReviewReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
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
                    table.PrimaryKey("PK_AppStudentAttendanceCorrections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppStudentAttendanceCorrections_AppAttendanceDays_Attendanc~",
                        column: x => x.AttendanceDayId,
                        principalTable: "AppAttendanceDays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppStudentAttendanceOperations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AttendanceDayId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResultJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppStudentAttendanceOperations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppStudentAttendanceOperations_AppAttendanceDays_Attendance~",
                        column: x => x.AttendanceDayId,
                        principalTable: "AppAttendanceDays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppStudentAttendanceSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    EnrollmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StartDate = table.Column<DateTime>(type: "date", nullable: false),
                    EndDate = table.Column<DateTime>(type: "date", nullable: true),
                    WeekDaysJson = table.Column<string>(type: "text", nullable: false),
                    StartTime = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    EndTime = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    RetiredFrom = table.Column<DateTime>(type: "date", nullable: true),
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
                    table.PrimaryKey("PK_AppStudentAttendanceSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppStudentAttendanceSchedules_AppStudentEnrollments_Enrollm~",
                        column: x => x.EnrollmentId,
                        principalTable: "AppStudentEnrollments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppStudentAttendancePolicyVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConfigurationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: true),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "date", nullable: false),
                    PolicyJson = table.Column<string>(type: "text", nullable: false),
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
                    table.PrimaryKey("PK_AppStudentAttendancePolicyVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppStudentAttendancePolicyVersions_AppStudentAttendanceConf~",
                        column: x => x.ConfigurationId,
                        principalTable: "AppStudentAttendanceConfigurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Existing active SIS students may predate formal enrollment activation.
            // Preserve their valid current placement without inventing an attendance start date.
            migrationBuilder.Sql("""
                INSERT INTO "AppStudentEnrollments"
                    ("Id", "TenantId", "StudentId", "AcademicYearId", "StandardId", "DivisionId", "RollNumber",
                     "IsActive", "ExtraProperties", "ConcurrencyStamp", "CreationTime", "IsDeleted",
                     "AttendancePlacementHistoryJson", "AttendanceStartDate")
                SELECT md5('AttendanceLegacyBridge|' || s."Id"::text || '|' || s."AcademicYearId"::text)::uuid,
                    s."TenantId", s."Id", s."AcademicYearId", s."StandardId", s."DivisionId",
                    CASE WHEN s."RollNo" > 0
                        AND NOT EXISTS (SELECT 1 FROM "AppStudentEnrollments" taken
                            WHERE taken."TenantId" = s."TenantId" AND taken."AcademicYearId" = s."AcademicYearId"
                                AND taken."DivisionId" = s."DivisionId" AND taken."IsActive" = true AND taken."RollNumber" = s."RollNo")
                        AND NOT EXISTS (SELECT 1 FROM "AppStudents" other
                            WHERE other."TenantId" = s."TenantId" AND other."AcademicYearId" = s."AcademicYearId"
                                AND other."DivisionId" = s."DivisionId" AND other."Status" = 6
                                AND other."IsDeleted" = false AND other."Id" <> s."Id" AND other."RollNo" = s."RollNo")
                        THEN s."RollNo" ELSE NULL END,
                    true, '{"AttendanceLegacyBridge":true}', md5(s."Id"::text || '|attendance'), now(), false, '[]', NULL
                FROM "AppStudents" s
                JOIN "academic_Divisions" division ON division."Id" = s."DivisionId" AND division."StandardId" = s."StandardId"
                    AND division."TenantId" = s."TenantId" AND division."IsDeleted" = false
                JOIN "academic_Standards" standard ON standard."Id" = s."StandardId"
                    AND standard."TenantId" = s."TenantId" AND standard."IsDeleted" = false
                JOIN "academic_EducationMediumStages" stage ON stage."Id" = standard."EducationMediumStageId"
                    AND stage."TenantId" = s."TenantId" AND stage."IsDeleted" = false
                JOIN "academic_EducationMediums" medium ON medium."Id" = stage."EducationMediumId" AND medium."Id" = s."AcademicMediumId"
                    AND medium."TenantId" = s."TenantId" AND medium."IsDeleted" = false
                JOIN "academic_EducationBoards" board ON board."Id" = medium."EducationBoardId" AND board."AcademicYearId" = s."AcademicYearId"
                    AND board."TenantId" = s."TenantId" AND board."IsDeleted" = false
                JOIN "academic_Years" year ON year."Id" = s."AcademicYearId" AND year."TenantId" = s."TenantId" AND year."IsDeleted" = false
                WHERE s."TenantId" IS NOT NULL AND s."Status" = 6 AND s."IsDeleted" = false
                    AND NOT EXISTS (SELECT 1 FROM "AppStudentEnrollments" existing
                        WHERE existing."TenantId" = s."TenantId" AND existing."StudentId" = s."Id" AND existing."AcademicYearId" = s."AcademicYearId");

                UPDATE "AppAttendanceEntries" entry
                SET "SchoolDate" = day."Date",
                    "PresentCredit" = CASE WHEN entry."Status" = 'Present' THEN 1.0 WHEN entry."Status" = 'Absent' THEN 0.0 ELSE NULL END
                FROM "AppAttendanceDays" day
                WHERE day."Id" = entry."AttendanceDayId" AND day."TenantId" IS NOT DISTINCT FROM entry."TenantId";

                -- An unambiguous identity link does not confirm historical roster membership or policy.
                -- Duplicate student/date outcomes across divisions stay unlinked for explicit review.
                WITH candidates AS (
                    SELECT entry."Id", enrollment."Id" AS enrollment_id,
                        count(*) OVER (PARTITION BY enrollment."Id", day."Date") AS date_outcomes
                    FROM "AppAttendanceEntries" entry
                    JOIN "AppAttendanceDays" day ON day."Id" = entry."AttendanceDayId" AND day."TenantId" = entry."TenantId" AND day."IsDeleted" = false
                    JOIN "AppStudentEnrollments" enrollment ON enrollment."StudentId" = entry."StudentId"
                        AND enrollment."AcademicYearId" = day."AcademicYearId" AND enrollment."TenantId" = entry."TenantId" AND enrollment."IsDeleted" = false
                    WHERE entry."IsDeleted" = false)
                UPDATE "AppAttendanceEntries" entry SET "EnrollmentId" = candidates.enrollment_id
                FROM candidates WHERE candidates."Id" = entry."Id" AND candidates.date_outcomes = 1;
                """);

            migrationBuilder.CreateIndex(
                name: "UX_AttendanceEntry_EnrollmentDate",
                table: "AppAttendanceEntries",
                columns: new[] { "EnrollmentId", "SchoolDate" },
                unique: true,
                filter: "\"EnrollmentId\" IS NOT NULL AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentAttendanceConfigurations_TenantId",
                table: "AppStudentAttendanceConfigurations",
                column: "TenantId",
                unique: true,
                filter: "\"AcademicYearId\" IS NULL AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentAttendanceConfigurations_TenantId_AcademicYearId",
                table: "AppStudentAttendanceConfigurations",
                columns: new[] { "TenantId", "AcademicYearId" },
                unique: true,
                filter: "\"AcademicYearId\" IS NOT NULL AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentAttendanceCorrections_AttendanceDayId",
                table: "AppStudentAttendanceCorrections",
                column: "AttendanceDayId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentAttendanceCorrections_TenantId_AttendanceDayId_St~",
                table: "AppStudentAttendanceCorrections",
                columns: new[] { "TenantId", "AttendanceDayId", "StudentId" },
                unique: true,
                filter: "\"Status\" = 'Pending' AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentAttendanceCorrections_TenantId_OperationId",
                table: "AppStudentAttendanceCorrections",
                columns: new[] { "TenantId", "OperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentAttendanceOperations_AttendanceDayId",
                table: "AppStudentAttendanceOperations",
                column: "AttendanceDayId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentAttendancePolicyVersions_ConfigurationId",
                table: "AppStudentAttendancePolicyVersions",
                column: "ConfigurationId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentAttendancePolicyVersions_TenantId_ConfigurationId~",
                table: "AppStudentAttendancePolicyVersions",
                columns: new[] { "TenantId", "ConfigurationId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentAttendanceSchedules_EnrollmentId",
                table: "AppStudentAttendanceSchedules",
                column: "EnrollmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentAttendanceSchedules_TenantId_EnrollmentId_StartDa~",
                table: "AppStudentAttendanceSchedules",
                columns: new[] { "TenantId", "EnrollmentId", "StartDate" });

            migrationBuilder.AddForeignKey(
                name: "FK_AppAttendanceEntries_AppStudentEnrollments_EnrollmentId",
                table: "AppAttendanceEntries",
                column: "EnrollmentId",
                principalTable: "AppStudentEnrollments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $attendance_rollback$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "AppStudentAttendanceConfigurations")
                        OR EXISTS (SELECT 1 FROM "AppStudentAttendanceCorrections")
                        OR EXISTS (SELECT 1 FROM "AppStudentAttendanceOperations")
                        OR EXISTS (SELECT 1 FROM "AppStudentAttendancePolicyVersions")
                        OR EXISTS (SELECT 1 FROM "AppStudentAttendanceSchedules")
                        OR EXISTS (SELECT 1 FROM "AppAttendanceEntries" WHERE "PolicySnapshotJson" IS NOT NULL)
                        OR EXISTS (SELECT 1 FROM "AppAttendanceDays" WHERE "IsSubmitted" = true)
                        OR EXISTS (SELECT 1 FROM "AppStudentEnrollments" WHERE "AttendanceStartDate" IS NOT NULL OR "AttendancePlacementHistoryJson" <> '[]') THEN
                        RAISE EXCEPTION 'Attendance rollback cannot discard policy, schedules, submissions or dated membership history; use a forward fix.';
                    END IF;
                END $attendance_rollback$;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_AppAttendanceEntries_AppStudentEnrollments_EnrollmentId",
                table: "AppAttendanceEntries");

            migrationBuilder.DropTable(
                name: "AppStudentAttendanceCorrections");

            migrationBuilder.DropTable(
                name: "AppStudentAttendanceOperations");

            migrationBuilder.DropTable(
                name: "AppStudentAttendancePolicyVersions");

            migrationBuilder.DropTable(
                name: "AppStudentAttendanceSchedules");

            migrationBuilder.DropTable(
                name: "AppStudentAttendanceConfigurations");

            migrationBuilder.DropIndex(
                name: "UX_AttendanceEntry_EnrollmentDate",
                table: "AppAttendanceEntries");

            migrationBuilder.DropColumn(
                name: "AttendancePlacementHistoryJson",
                table: "AppStudentEnrollments");

            migrationBuilder.DropColumn(
                name: "AttendanceStartDate",
                table: "AppStudentEnrollments");

            migrationBuilder.DropColumn(
                name: "ApprovedLeaveId",
                table: "AppAttendanceEntries");

            migrationBuilder.DropColumn(
                name: "DenominatorCredit",
                table: "AppAttendanceEntries");

            migrationBuilder.DropColumn(
                name: "EnrollmentId",
                table: "AppAttendanceEntries");

            migrationBuilder.DropColumn(
                name: "ExcludedFromPercentage",
                table: "AppAttendanceEntries");

            migrationBuilder.DropColumn(
                name: "ExpectedAtSubmission",
                table: "AppAttendanceEntries");

            migrationBuilder.DropColumn(
                name: "HalfDay",
                table: "AppAttendanceEntries");

            migrationBuilder.DropColumn(
                name: "HistoricalRollNo",
                table: "AppAttendanceEntries");

            migrationBuilder.DropColumn(
                name: "Late",
                table: "AppAttendanceEntries");

            migrationBuilder.DropColumn(
                name: "PolicySnapshotJson",
                table: "AppAttendanceEntries");

            migrationBuilder.DropColumn(
                name: "PresentCredit",
                table: "AppAttendanceEntries");

            migrationBuilder.DropColumn(
                name: "SchoolDate",
                table: "AppAttendanceEntries");

            migrationBuilder.DropColumn(
                name: "IsSubmitted",
                table: "AppAttendanceDays");

            migrationBuilder.DropColumn(
                name: "LastOperationHash",
                table: "AppAttendanceDays");

            migrationBuilder.DropColumn(
                name: "LastOperationId",
                table: "AppAttendanceDays");

            migrationBuilder.DropColumn(
                name: "PolicySnapshotJson",
                table: "AppAttendanceDays");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "AppAttendanceDays");

            migrationBuilder.DropColumn(
                name: "SubmittedRosterRevision",
                table: "AppAttendanceDays");
        }
    }
}
