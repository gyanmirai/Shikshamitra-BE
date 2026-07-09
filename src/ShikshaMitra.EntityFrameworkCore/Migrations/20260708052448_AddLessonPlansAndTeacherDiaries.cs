using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddLessonPlansAndTeacherDiaries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppLessonPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: false),
                    StandardId = table.Column<Guid>(type: "uuid", nullable: false),
                    DivisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChapterId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlannedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TimetableSlotId = table.Column<Guid>(type: "uuid", nullable: true),
                    Topic = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    LearningObjective = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    TeachingMethod = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ResourcesRequired = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    PlannedCompletionPercentage = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ReviewRemarks = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    ReviewedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsAiGenerated = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_AppLessonPlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppTeacherDiaries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    LessonPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActualDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClassConducted = table.Column<bool>(type: "boolean", nullable: false),
                    ActualTopicCovered = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ActualContentSummary = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    ActualCompletionPercentage = table.Column<int>(type: "integer", nullable: false),
                    TeachingMethodUsed = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    StudentResponse = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    ClassUnderstandingLevel = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    StudentEngagementLevel = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    NeedRevision = table.Column<bool>(type: "boolean", nullable: false),
                    Reflection = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    WhatWentWell = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    WhatWasDifficult = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    StudentsNeedingSupport = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    NextClassFollowUp = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    RemedialActionRequired = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    HomeworkAssigned = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    MissedReason = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    RescheduledToDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsBackdated = table.Column<bool>(type: "boolean", nullable: false),
                    BackdatedReason = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    TeacherRemarks = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_AppTeacherDiaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppTeacherDiaries_AppLessonPlans_LessonPlanId",
                        column: x => x.LessonPlanId,
                        principalTable: "AppLessonPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppLessonPlans_TeacherId_PlannedDate",
                table: "AppLessonPlans",
                columns: new[] { "TeacherId", "PlannedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AppLessonPlans_TimetableSlotId",
                table: "AppLessonPlans",
                column: "TimetableSlotId");

            migrationBuilder.CreateIndex(
                name: "IX_AppTeacherDiaries_LessonPlanId",
                table: "AppTeacherDiaries",
                column: "LessonPlanId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppTeacherDiaries_TeacherId_ActualDate",
                table: "AppTeacherDiaries",
                columns: new[] { "TeacherId", "ActualDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppTeacherDiaries");

            migrationBuilder.DropTable(
                name: "AppLessonPlans");
        }
    }
}
