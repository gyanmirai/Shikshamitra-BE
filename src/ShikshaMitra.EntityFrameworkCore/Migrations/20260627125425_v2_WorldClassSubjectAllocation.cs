using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v2_WorldClassSubjectAllocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssessmentMethod",
                table: "AppSubjectChapters",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssessmentStatus",
                table: "AppSubjectChapters",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AudioVideoMaterials",
                table: "AppSubjectChapters",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BloomCoverage",
                table: "AppSubjectChapters",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BloomLevel",
                table: "AppSubjectChapters",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BoardDocuments",
                table: "AppSubjectChapters",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BufferDays",
                table: "AppSubjectChapters",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClassActivities",
                table: "AppSubjectChapters",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompletionCriteria",
                table: "AppSubjectChapters",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CopiedFromPreviousYear",
                table: "AppSubjectChapters",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CoverageStatus",
                table: "AppSubjectChapters",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DependencyChapterIds",
                table: "AppSubjectChapters",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DifficultyDistribution",
                table: "AppSubjectChapters",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DifficultyLevel",
                table: "AppSubjectChapters",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EstimatedPeriods",
                table: "AppSubjectChapters",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EstimatedTeachingDays",
                table: "AppSubjectChapters",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HealthScore",
                table: "AppSubjectChapters",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "HomeworkStatus",
                table: "AppSubjectChapters",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAiGenerated",
                table: "AppSubjectChapters",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LearningOutcomes",
                table: "AppSubjectChapters",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LessonPlanStatus",
                table: "AppSubjectChapters",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MarksWeightage",
                table: "AppSubjectChapters",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyOnPublish",
                table: "AppSubjectChapters",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ParentSummary",
                table: "AppSubjectChapters",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Prerequisites",
                table: "AppSubjectChapters",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProgressPercentage",
                table: "AppSubjectChapters",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PublishStatus",
                table: "AppSubjectChapters",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "QuestionCount",
                table: "AppSubjectChapters",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "QuestionTypes",
                table: "AppSubjectChapters",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferenceLinks",
                table: "AppSubjectChapters",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResourceStatus",
                table: "AppSubjectChapters",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewStatus",
                table: "AppSubjectChapters",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RevisionDays",
                table: "AppSubjectChapters",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SkillsCovered",
                table: "AppSubjectChapters",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentSummary",
                table: "AppSubjectChapters",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tags",
                table: "AppSubjectChapters",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TeacherNotes",
                table: "AppSubjectChapters",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TeachingMethods",
                table: "AppSubjectChapters",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TemplateSource",
                table: "AppSubjectChapters",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TermName",
                table: "AppSubjectChapters",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Topic",
                table: "AppSubjectChapters",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnitName",
                table: "AppSubjectChapters",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VersionLabel",
                table: "AppSubjectChapters",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Visibility",
                table: "AppSubjectChapters",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Worksheets",
                table: "AppSubjectChapters",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DifficultyLevel",
                table: "AppStandardSubjectAssignments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsAiSuggested",
                table: "AppStandardSubjectAssignments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsCopiedFromPreviousYear",
                table: "AppStandardSubjectAssignments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "SetupHealthScore",
                table: "AppStandardSubjectAssignments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SubjectGroup",
                table: "AppStandardSubjectAssignments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WeeklyPeriods",
                table: "AppStandardSubjectAssignments",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssessmentMethod",
                table: "AppSubjectChapters");

            migrationBuilder.DropColumn(
                name: "AssessmentStatus",
                table: "AppSubjectChapters");

            migrationBuilder.DropColumn(
                name: "AudioVideoMaterials",
                table: "AppSubjectChapters");

            migrationBuilder.DropColumn(
                name: "BloomCoverage",
                table: "AppSubjectChapters");

            migrationBuilder.DropColumn(
                name: "BloomLevel",
                table: "AppSubjectChapters");

            migrationBuilder.DropColumn(
                name: "BoardDocuments",
                table: "AppSubjectChapters");

            migrationBuilder.DropColumn(
                name: "BufferDays",
                table: "AppSubjectChapters");

            migrationBuilder.DropColumn(
                name: "ClassActivities",
                table: "AppSubjectChapters");

            migrationBuilder.DropColumn(
                name: "CompletionCriteria",
                table: "AppSubjectChapters");

            migrationBuilder.DropColumn(
                name: "CopiedFromPreviousYear",
                table: "AppSubjectChapters");

            migrationBuilder.DropColumn(
                name: "CoverageStatus",
                table: "AppSubjectChapters");

            migrationBuilder.DropColumn(
                name: "DependencyChapterIds",
                table: "AppSubjectChapters");

            migrationBuilder.DropColumn(
                name: "DifficultyDistribution",
                table: "AppSubjectChapters");

            migrationBuilder.DropColumn(
                name: "DifficultyLevel",
                table: "AppSubjectChapters");

            migrationBuilder.DropColumn(
                name: "EstimatedPeriods",
                table: "AppSubjectChapters");

            migrationBuilder.DropColumn(
                name: "EstimatedTeachingDays",
                table: "AppSubjectChapters");

            migrationBuilder.DropColumn(
                name: "HealthScore",
                table: "AppSubjectChapters");

            migrationBuilder.DropColumn(
                name: "HomeworkStatus",
                table: "AppSubjectChapters");

            migrationBuilder.DropColumn(
                name: "IsAiGenerated",
                table: "AppSubjectChapters");

            migrationBuilder.DropColumn(
                name: "LearningOutcomes",
                table: "AppSubjectChapters");

            migrationBuilder.DropColumn(
                name: "LessonPlanStatus",
                table: "AppSubjectChapters");

            migrationBuilder.DropColumn(
                name: "MarksWeightage",
                table: "AppSubjectChapters");

            migrationBuilder.DropColumn(
                name: "NotifyOnPublish",
                table: "AppSubjectChapters");

            migrationBuilder.DropColumn(
                name: "ParentSummary",
                table: "AppSubjectChapters");

            migrationBuilder.DropColumn(
                name: "Prerequisites",
                table: "AppSubjectChapters");

            migrationBuilder.DropColumn(
                name: "ProgressPercentage",
                table: "AppSubjectChapters");

            migrationBuilder.DropColumn(
                name: "PublishStatus",
                table: "AppSubjectChapters");

            migrationBuilder.DropColumn(
                name: "QuestionCount",
                table: "AppSubjectChapters");

            migrationBuilder.DropColumn(
                name: "QuestionTypes",
                table: "AppSubjectChapters");

            migrationBuilder.DropColumn(
                name: "ReferenceLinks",
                table: "AppSubjectChapters");

            migrationBuilder.DropColumn(
                name: "ResourceStatus",
                table: "AppSubjectChapters");

            migrationBuilder.DropColumn(
                name: "ReviewStatus",
                table: "AppSubjectChapters");

            migrationBuilder.DropColumn(
                name: "RevisionDays",
                table: "AppSubjectChapters");

            migrationBuilder.DropColumn(
                name: "SkillsCovered",
                table: "AppSubjectChapters");

            migrationBuilder.DropColumn(
                name: "StudentSummary",
                table: "AppSubjectChapters");

            migrationBuilder.DropColumn(
                name: "Tags",
                table: "AppSubjectChapters");

            migrationBuilder.DropColumn(
                name: "TeacherNotes",
                table: "AppSubjectChapters");

            migrationBuilder.DropColumn(
                name: "TeachingMethods",
                table: "AppSubjectChapters");

            migrationBuilder.DropColumn(
                name: "TemplateSource",
                table: "AppSubjectChapters");

            migrationBuilder.DropColumn(
                name: "TermName",
                table: "AppSubjectChapters");

            migrationBuilder.DropColumn(
                name: "Topic",
                table: "AppSubjectChapters");

            migrationBuilder.DropColumn(
                name: "UnitName",
                table: "AppSubjectChapters");

            migrationBuilder.DropColumn(
                name: "VersionLabel",
                table: "AppSubjectChapters");

            migrationBuilder.DropColumn(
                name: "Visibility",
                table: "AppSubjectChapters");

            migrationBuilder.DropColumn(
                name: "Worksheets",
                table: "AppSubjectChapters");

            migrationBuilder.DropColumn(
                name: "DifficultyLevel",
                table: "AppStandardSubjectAssignments");

            migrationBuilder.DropColumn(
                name: "IsAiSuggested",
                table: "AppStandardSubjectAssignments");

            migrationBuilder.DropColumn(
                name: "IsCopiedFromPreviousYear",
                table: "AppStandardSubjectAssignments");

            migrationBuilder.DropColumn(
                name: "SetupHealthScore",
                table: "AppStandardSubjectAssignments");

            migrationBuilder.DropColumn(
                name: "SubjectGroup",
                table: "AppStandardSubjectAssignments");

            migrationBuilder.DropColumn(
                name: "WeeklyPeriods",
                table: "AppStandardSubjectAssignments");
        }
    }
}
