using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddRubricAssessments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppAssessmentRubrics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StandardId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    HomeworkId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    TotalMarks = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false, defaultValue: "{}"),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, defaultValue: ""),
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
                    table.PrimaryKey("PK_AppAssessmentRubrics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppRubricEvaluations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    RubricId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentSubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvaluatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    TotalMarks = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Remarks = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false, defaultValue: "{}"),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, defaultValue: ""),
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
                    table.PrimaryKey("PK_AppRubricEvaluations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppAssessmentRubricCriteria",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    RubricId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    MaxMarks = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_AppAssessmentRubricCriteria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppAssessmentRubricCriteria_AppAssessmentRubrics_RubricId",
                        column: x => x.RubricId,
                        principalTable: "AppAssessmentRubrics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppRubricCriterionScores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    EvaluationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CriterionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Marks = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Comments = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
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
                    table.PrimaryKey("PK_AppRubricCriterionScores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppRubricCriterionScores_AppRubricEvaluations_EvaluationId",
                        column: x => x.EvaluationId,
                        principalTable: "AppRubricEvaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppAssessmentRubricCriteria_RubricId",
                table: "AppAssessmentRubricCriteria",
                column: "RubricId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAssessmentRubricCriteria_RubricId_DisplayOrder",
                table: "AppAssessmentRubricCriteria",
                columns: new[] { "RubricId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_AppAssessmentRubrics_AssignmentId",
                table: "AppAssessmentRubrics",
                column: "AssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAssessmentRubrics_HomeworkId",
                table: "AppAssessmentRubrics",
                column: "HomeworkId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAssessmentRubrics_StandardId_SubjectId",
                table: "AppAssessmentRubrics",
                columns: new[] { "StandardId", "SubjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppAssessmentRubrics_Status",
                table: "AppAssessmentRubrics",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AppRubricCriterionScores_EvaluationId",
                table: "AppRubricCriterionScores",
                column: "EvaluationId");

            migrationBuilder.CreateIndex(
                name: "IX_AppRubricCriterionScores_EvaluationId_CriterionId",
                table: "AppRubricCriterionScores",
                columns: new[] { "EvaluationId", "CriterionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppRubricEvaluations_RubricId",
                table: "AppRubricEvaluations",
                column: "RubricId");

            migrationBuilder.CreateIndex(
                name: "IX_AppRubricEvaluations_RubricId_StudentSubmissionId",
                table: "AppRubricEvaluations",
                columns: new[] { "RubricId", "StudentSubmissionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppRubricEvaluations_Status",
                table: "AppRubricEvaluations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AppRubricEvaluations_StudentSubmissionId",
                table: "AppRubricEvaluations",
                column: "StudentSubmissionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppAssessmentRubricCriteria");

            migrationBuilder.DropTable(
                name: "AppRubricCriterionScores");

            migrationBuilder.DropTable(
                name: "AppAssessmentRubrics");

            migrationBuilder.DropTable(
                name: "AppRubricEvaluations");
        }
    }
}
