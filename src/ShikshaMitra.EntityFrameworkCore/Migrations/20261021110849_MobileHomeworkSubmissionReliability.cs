using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class MobileHomeworkSubmissionReliability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConcurrencyStamp",
                table: "academic_StudentHomeworkAssignmentSubmissions",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValueSql: "md5(random()::text || clock_timestamp()::text)");

            migrationBuilder.Sql("UPDATE \"academic_StudentHomeworkAssignmentSubmissions\" SET \"ConcurrencyStamp\" = md5(random()::text || clock_timestamp()::text || \"Id\"::text) WHERE \"ConcurrencyStamp\" = ''");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "MobileSavedAt",
                table: "academic_StudentHomeworkAssignmentSubmissions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "MobileUpdatedAt",
                table: "academic_StudentHomeworkAssignmentSubmissions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubmittedContentHtml",
                table: "academic_StudentHomeworkAssignmentSubmissions",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "academic_MobileSubmissionOperations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    Operation = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    PayloadHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResponseJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_academic_MobileSubmissionOperations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "academic_MobileSubmissionSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TextAnswer = table.Column<string>(type: "text", nullable: true),
                    TextAnswerHtml = table.Column<string>(type: "text", nullable: true),
                    AttachmentsJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_academic_MobileSubmissionSnapshots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_academic_MobileSubmissionOperations_TenantId_ActorId_Studen~",
                table: "academic_MobileSubmissionOperations",
                columns: new[] { "TenantId", "ActorId", "StudentId", "AcademicYearId", "TaskId", "Operation", "RequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_academic_MobileSubmissionSnapshots_SubmissionId_Revision",
                table: "academic_MobileSubmissionSnapshots",
                columns: new[] { "SubmissionId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_academic_MobileSubmissionSnapshots_TenantId_StudentId_TaskId",
                table: "academic_MobileSubmissionSnapshots",
                columns: new[] { "TenantId", "StudentId", "TaskId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "academic_MobileSubmissionOperations");

            migrationBuilder.DropTable(
                name: "academic_MobileSubmissionSnapshots");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                table: "academic_StudentHomeworkAssignmentSubmissions");

            migrationBuilder.DropColumn(
                name: "MobileSavedAt",
                table: "academic_StudentHomeworkAssignmentSubmissions");

            migrationBuilder.DropColumn(
                name: "MobileUpdatedAt",
                table: "academic_StudentHomeworkAssignmentSubmissions");

            migrationBuilder.DropColumn(
                name: "SubmittedContentHtml",
                table: "academic_StudentHomeworkAssignmentSubmissions");
        }
    }
}
