using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class PerformanceManagementV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AcademicYearId",
                table: "AppProfessionalReviewCycles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AllowNotApplicable",
                table: "AppProfessionalReviewCycles",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "AudienceFilterJson",
                table: "AppProfessionalReviewCycles",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CycleType",
                table: "AppProfessionalReviewCycles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "FinalizationDueDate",
                table: "AppProfessionalReviewCycles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RatingScaleMax",
                table: "AppProfessionalReviewCycles",
                type: "integer",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewerDueDate",
                table: "AppProfessionalReviewCycles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewerWorkflowJson",
                table: "AppProfessionalReviewCycles",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SelfReviewDueDate",
                table: "AppProfessionalReviewCycles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppClassroomObservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewAssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ObserverEmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ObservationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClassName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    SubjectName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    LessonObjectiveScore = table.Column<int>(type: "integer", nullable: false),
                    StudentEngagementScore = table.Column<int>(type: "integer", nullable: false),
                    ClassroomManagementScore = table.Column<int>(type: "integer", nullable: false),
                    AssessmentScore = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_AppClassroomObservations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppDevelopmentPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewAssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Goal = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    ActionPlan = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    OwnerEmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_AppDevelopmentPlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppPerformanceAuditEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewAssignmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    MetadataJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppPerformanceAuditEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppPerformanceEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewAssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CriterionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: true),
                    Url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_AppPerformanceEvidence", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppPerformanceFeedback",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewAssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    ProviderName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    FeedbackText = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
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
                    table.PrimaryKey("PK_AppPerformanceFeedback", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppPerformanceOutcomes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewAssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    FinalScore = table.Column<decimal>(type: "numeric(7,2)", precision: 7, scale: 2, nullable: false),
                    FinalRating = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Strengths = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ImprovementAreas = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Recommendation = table.Column<int>(type: "integer", nullable: false),
                    RecommendationNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IncrementRecommended = table.Column<bool>(type: "boolean", nullable: false),
                    PromotionRecommended = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_AppPerformanceOutcomes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppPerformanceReviewAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CycleId = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportingManagerEmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    HodEmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    PrincipalEmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    FinalScore = table.Column<decimal>(type: "numeric(7,2)", precision: 7, scale: 2, nullable: true),
                    FinalRating = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    FinalizedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_AppPerformanceReviewAssignments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppPerformanceReviewResponses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewAssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    StageId = table.Column<Guid>(type: "uuid", nullable: false),
                    CriterionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: true),
                    IsNotApplicable = table.Column<bool>(type: "boolean", nullable: false),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_AppPerformanceReviewResponses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppPerformanceReviewStages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewAssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    StageType = table.Column<int>(type: "integer", nullable: false),
                    ReviewerEmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SummaryComment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_AppPerformanceReviewStages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppPerformanceTemplateCriteria",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Weight = table.Column<decimal>(type: "numeric(7,2)", precision: 7, scale: 2, nullable: false),
                    EvidenceRequired = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_AppPerformanceTemplateCriteria", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppPerformanceTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    StaffCategory = table.Column<int>(type: "integer", nullable: false),
                    RoleName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    RatingScaleMax = table.Column<int>(type: "integer", nullable: false),
                    AllowNotApplicable = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_AppPerformanceTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppPerformanceTemplateSections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Weight = table.Column<decimal>(type: "numeric(7,2)", precision: 7, scale: 2, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_AppPerformanceTemplateSections", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppClassroomObservations_TenantId_ReviewAssignmentId_Observ~",
                table: "AppClassroomObservations",
                columns: new[] { "TenantId", "ReviewAssignmentId", "ObservationDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AppDevelopmentPlans_TenantId_ReviewAssignmentId_Status",
                table: "AppDevelopmentPlans",
                columns: new[] { "TenantId", "ReviewAssignmentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AppPerformanceAuditEvents_TenantId_ReviewAssignmentId_Creat~",
                table: "AppPerformanceAuditEvents",
                columns: new[] { "TenantId", "ReviewAssignmentId", "CreationTime" });

            migrationBuilder.CreateIndex(
                name: "IX_AppPerformanceEvidence_TenantId_ReviewAssignmentId",
                table: "AppPerformanceEvidence",
                columns: new[] { "TenantId", "ReviewAssignmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppPerformanceFeedback_TenantId_ReviewAssignmentId_Source",
                table: "AppPerformanceFeedback",
                columns: new[] { "TenantId", "ReviewAssignmentId", "Source" });

            migrationBuilder.CreateIndex(
                name: "IX_AppPerformanceOutcomes_TenantId_ReviewAssignmentId",
                table: "AppPerformanceOutcomes",
                columns: new[] { "TenantId", "ReviewAssignmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppPerformanceReviewAssignments_TenantId_CycleId_EmployeeId",
                table: "AppPerformanceReviewAssignments",
                columns: new[] { "TenantId", "CycleId", "EmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppPerformanceReviewAssignments_TenantId_Status",
                table: "AppPerformanceReviewAssignments",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AppPerformanceReviewResponses_TenantId_StageId_CriterionId",
                table: "AppPerformanceReviewResponses",
                columns: new[] { "TenantId", "StageId", "CriterionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppPerformanceReviewStages_TenantId_ReviewAssignmentId_Stag~",
                table: "AppPerformanceReviewStages",
                columns: new[] { "TenantId", "ReviewAssignmentId", "StageType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppPerformanceTemplateCriteria_TenantId_TemplateId_SectionI~",
                table: "AppPerformanceTemplateCriteria",
                columns: new[] { "TenantId", "TemplateId", "SectionId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_AppPerformanceTemplates_TenantId_StaffCategory_Status",
                table: "AppPerformanceTemplates",
                columns: new[] { "TenantId", "StaffCategory", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AppPerformanceTemplateSections_TenantId_TemplateId_DisplayO~",
                table: "AppPerformanceTemplateSections",
                columns: new[] { "TenantId", "TemplateId", "DisplayOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppClassroomObservations");

            migrationBuilder.DropTable(
                name: "AppDevelopmentPlans");

            migrationBuilder.DropTable(
                name: "AppPerformanceAuditEvents");

            migrationBuilder.DropTable(
                name: "AppPerformanceEvidence");

            migrationBuilder.DropTable(
                name: "AppPerformanceFeedback");

            migrationBuilder.DropTable(
                name: "AppPerformanceOutcomes");

            migrationBuilder.DropTable(
                name: "AppPerformanceReviewAssignments");

            migrationBuilder.DropTable(
                name: "AppPerformanceReviewResponses");

            migrationBuilder.DropTable(
                name: "AppPerformanceReviewStages");

            migrationBuilder.DropTable(
                name: "AppPerformanceTemplateCriteria");

            migrationBuilder.DropTable(
                name: "AppPerformanceTemplates");

            migrationBuilder.DropTable(
                name: "AppPerformanceTemplateSections");

            migrationBuilder.DropColumn(
                name: "AcademicYearId",
                table: "AppProfessionalReviewCycles");

            migrationBuilder.DropColumn(
                name: "AllowNotApplicable",
                table: "AppProfessionalReviewCycles");

            migrationBuilder.DropColumn(
                name: "AudienceFilterJson",
                table: "AppProfessionalReviewCycles");

            migrationBuilder.DropColumn(
                name: "CycleType",
                table: "AppProfessionalReviewCycles");

            migrationBuilder.DropColumn(
                name: "FinalizationDueDate",
                table: "AppProfessionalReviewCycles");

            migrationBuilder.DropColumn(
                name: "RatingScaleMax",
                table: "AppProfessionalReviewCycles");

            migrationBuilder.DropColumn(
                name: "ReviewerDueDate",
                table: "AppProfessionalReviewCycles");

            migrationBuilder.DropColumn(
                name: "ReviewerWorkflowJson",
                table: "AppProfessionalReviewCycles");

            migrationBuilder.DropColumn(
                name: "SelfReviewDueDate",
                table: "AppProfessionalReviewCycles");
        }
    }
}
