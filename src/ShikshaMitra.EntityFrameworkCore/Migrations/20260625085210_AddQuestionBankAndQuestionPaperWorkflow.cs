using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddQuestionBankAndQuestionPaperWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppQuestionBankItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    BoardId = table.Column<Guid>(type: "uuid", nullable: true),
                    MediumId = table.Column<Guid>(type: "uuid", nullable: true),
                    StandardId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChapterId = table.Column<Guid>(type: "uuid", nullable: true),
                    TopicId = table.Column<Guid>(type: "uuid", nullable: true),
                    QuestionText = table.Column<string>(type: "text", nullable: false),
                    PlainTextSearchContent = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    QuestionType = table.Column<int>(type: "integer", nullable: false),
                    DifficultyLevel = table.Column<int>(type: "integer", nullable: false),
                    BloomLevel = table.Column<int>(type: "integer", nullable: true),
                    Marks = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    NegativeMarks = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    AnswerKey = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    LearningOutcome = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Explanation = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    MarkingScheme = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Visibility = table.Column<int>(type: "integer", nullable: false),
                    CreatedByTeacherId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewComment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    ParentQuestionId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsLocked = table.Column<bool>(type: "boolean", nullable: false),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_AppQuestionBankItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppQuestionBankSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovalMode = table.Column<int>(type: "integer", nullable: false),
                    DefaultVisibility = table.Column<int>(type: "integer", nullable: false),
                    EnableMcq = table.Column<bool>(type: "boolean", nullable: false),
                    EnableTrueFalse = table.Column<bool>(type: "boolean", nullable: false),
                    EnableFillInTheBlank = table.Column<bool>(type: "boolean", nullable: false),
                    EnableSubjective = table.Column<bool>(type: "boolean", nullable: false),
                    EnableMatchTheFollowing = table.Column<bool>(type: "boolean", nullable: false),
                    EnableAssertionReason = table.Column<bool>(type: "boolean", nullable: false),
                    EnableCaseStudy = table.Column<bool>(type: "boolean", nullable: false),
                    EnableDiagramBased = table.Column<bool>(type: "boolean", nullable: false),
                    RequireChapter = table.Column<bool>(type: "boolean", nullable: false),
                    RequireTopic = table.Column<bool>(type: "boolean", nullable: false),
                    RequireDifficulty = table.Column<bool>(type: "boolean", nullable: false),
                    RequireBloomLevel = table.Column<bool>(type: "boolean", nullable: false),
                    RequireAnswerKey = table.Column<bool>(type: "boolean", nullable: false),
                    RequireLearningOutcome = table.Column<bool>(type: "boolean", nullable: false),
                    AllowDecimalMarks = table.Column<bool>(type: "boolean", nullable: false),
                    EnableNegativeMarking = table.Column<bool>(type: "boolean", nullable: false),
                    EnableDuplicateCheck = table.Column<bool>(type: "boolean", nullable: false),
                    DuplicateQuestionAction = table.Column<int>(type: "integer", nullable: false),
                    EnableQuestionVersioning = table.Column<bool>(type: "boolean", nullable: false),
                    EnableBulkImport = table.Column<bool>(type: "boolean", nullable: false),
                    EnableRichTextEditor = table.Column<bool>(type: "boolean", nullable: false),
                    EnableLatexEquation = table.Column<bool>(type: "boolean", nullable: false),
                    EnableImageAttachment = table.Column<bool>(type: "boolean", nullable: false),
                    MaxAttachmentSizeMb = table.Column<int>(type: "integer", nullable: false),
                    EnableAiQuestionGenerator = table.Column<bool>(type: "boolean", nullable: false),
                    EnableAiBloomClassifier = table.Column<bool>(type: "boolean", nullable: false),
                    EnableAiDuplicateDetector = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_AppQuestionBankSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppQuestionPaperBlueprints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    QuestionPaperId = table.Column<Guid>(type: "uuid", nullable: false),
                    TotalMarks = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
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
                    table.PrimaryKey("PK_AppQuestionPaperBlueprints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppQuestionPapers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExamId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExamScheduleId = table.Column<Guid>(type: "uuid", nullable: true),
                    StandardId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaperTitle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    TotalMarks = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    CreationMode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    GeneralInstructions = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    ParentPaperId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsLocked = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_AppQuestionPapers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppQuestionPaperSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequireApprovedQuestions = table.Column<bool>(type: "boolean", nullable: false),
                    EnableBlueprintValidation = table.Column<bool>(type: "boolean", nullable: false),
                    EnableRecentQuestionBlock = table.Column<bool>(type: "boolean", nullable: false),
                    RecentQuestionBlockDays = table.Column<int>(type: "integer", nullable: false),
                    DistributionTolerancePercentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    EnablePdfCompilation = table.Column<bool>(type: "boolean", nullable: false),
                    LockAfterPublish = table.Column<bool>(type: "boolean", nullable: false),
                    EnableAiBlueprintGenerator = table.Column<bool>(type: "boolean", nullable: false),
                    EnableAiPaperGenerator = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_AppQuestionPaperSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppQuestionUsageLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionBankItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    SourceEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_AppQuestionUsageLogs", x => x.Id);
                });



            migrationBuilder.CreateTable(
                name: "AppQuestionAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionBankItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FileUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    MimeType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    AttachmentType = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_AppQuestionAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppQuestionAttachments_AppQuestionBankItems_QuestionBankIte~",
                        column: x => x.QuestionBankItemId,
                        principalTable: "AppQuestionBankItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppQuestionOptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionBankItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    OptionText = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_AppQuestionOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppQuestionOptions_AppQuestionBankItems_QuestionBankItemId",
                        column: x => x.QuestionBankItemId,
                        principalTable: "AppQuestionBankItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppQuestionTags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionBankItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    TagName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("PK_AppQuestionTags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppQuestionTags_AppQuestionBankItems_QuestionBankItemId",
                        column: x => x.QuestionBankItemId,
                        principalTable: "AppQuestionBankItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppQuestionPaperBlueprintBloomRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionPaperBlueprintId = table.Column<Guid>(type: "uuid", nullable: false),
                    BloomLevel = table.Column<int>(type: "integer", nullable: false),
                    Percentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
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
                    table.PrimaryKey("PK_AppQuestionPaperBlueprintBloomRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppQuestionPaperBlueprintBloomRules_AppQuestionPaperBluepri~",
                        column: x => x.QuestionPaperBlueprintId,
                        principalTable: "AppQuestionPaperBlueprints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppQuestionPaperBlueprintChapterRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionPaperBlueprintId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChapterId = table.Column<Guid>(type: "uuid", nullable: false),
                    Marks = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    NumberOfQuestions = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_AppQuestionPaperBlueprintChapterRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppQuestionPaperBlueprintChapterRules_AppQuestionPaperBluep~",
                        column: x => x.QuestionPaperBlueprintId,
                        principalTable: "AppQuestionPaperBlueprints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppQuestionPaperBlueprintDifficultyRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionPaperBlueprintId = table.Column<Guid>(type: "uuid", nullable: false),
                    DifficultyLevel = table.Column<int>(type: "integer", nullable: false),
                    Percentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
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
                    table.PrimaryKey("PK_AppQuestionPaperBlueprintDifficultyRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppQuestionPaperBlueprintDifficultyRules_AppQuestionPaperBl~",
                        column: x => x.QuestionPaperBlueprintId,
                        principalTable: "AppQuestionPaperBlueprints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppQuestionPaperQuestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionPaperId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionPaperSectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionBankItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionNumber = table.Column<int>(type: "integer", nullable: false),
                    Marks = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    ChoiceGroupCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
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
                    table.PrimaryKey("PK_AppQuestionPaperQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppQuestionPaperQuestions_AppQuestionPapers_QuestionPaperId",
                        column: x => x.QuestionPaperId,
                        principalTable: "AppQuestionPapers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppQuestionPaperSections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionPaperId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Instructions = table.Column<string>(type: "text", nullable: true),
                    Marks = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_AppQuestionPaperSections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppQuestionPaperSections_AppQuestionPapers_QuestionPaperId",
                        column: x => x.QuestionPaperId,
                        principalTable: "AppQuestionPapers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppQuestionAttachments_QuestionBankItemId",
                table: "AppQuestionAttachments",
                column: "QuestionBankItemId");

            migrationBuilder.CreateIndex(
                name: "IX_AppQuestionBankItems_AcademicYearId",
                table: "AppQuestionBankItems",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_AppQuestionBankItems_BloomLevel",
                table: "AppQuestionBankItems",
                column: "BloomLevel");

            migrationBuilder.CreateIndex(
                name: "IX_AppQuestionBankItems_ChapterId",
                table: "AppQuestionBankItems",
                column: "ChapterId");

            migrationBuilder.CreateIndex(
                name: "IX_AppQuestionBankItems_CreatedByTeacherId",
                table: "AppQuestionBankItems",
                column: "CreatedByTeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_AppQuestionBankItems_DifficultyLevel",
                table: "AppQuestionBankItems",
                column: "DifficultyLevel");

            migrationBuilder.CreateIndex(
                name: "IX_AppQuestionBankItems_QuestionType",
                table: "AppQuestionBankItems",
                column: "QuestionType");

            migrationBuilder.CreateIndex(
                name: "IX_AppQuestionBankItems_StandardId",
                table: "AppQuestionBankItems",
                column: "StandardId");

            migrationBuilder.CreateIndex(
                name: "IX_AppQuestionBankItems_Status",
                table: "AppQuestionBankItems",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AppQuestionBankItems_SubjectId",
                table: "AppQuestionBankItems",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_AppQuestionBankItems_TopicId",
                table: "AppQuestionBankItems",
                column: "TopicId");

            migrationBuilder.CreateIndex(
                name: "IX_AppQuestionBankItems_Visibility",
                table: "AppQuestionBankItems",
                column: "Visibility");

            migrationBuilder.CreateIndex(
                name: "IX_AppQuestionBankSettings_AcademicYearId",
                table: "AppQuestionBankSettings",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_AppQuestionBankSettings_TenantId_AcademicYearId",
                table: "AppQuestionBankSettings",
                columns: new[] { "TenantId", "AcademicYearId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppQuestionOptions_QuestionBankItemId_SortOrder",
                table: "AppQuestionOptions",
                columns: new[] { "QuestionBankItemId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppQuestionPaperBlueprintBloomRules_QuestionPaperBlueprintId",
                table: "AppQuestionPaperBlueprintBloomRules",
                column: "QuestionPaperBlueprintId");

            migrationBuilder.CreateIndex(
                name: "IX_AppQuestionPaperBlueprintChapterRules_QuestionPaperBlueprin~",
                table: "AppQuestionPaperBlueprintChapterRules",
                column: "QuestionPaperBlueprintId");

            migrationBuilder.CreateIndex(
                name: "IX_AppQuestionPaperBlueprintDifficultyRules_QuestionPaperBluep~",
                table: "AppQuestionPaperBlueprintDifficultyRules",
                column: "QuestionPaperBlueprintId");

            migrationBuilder.CreateIndex(
                name: "IX_AppQuestionPaperBlueprints_QuestionPaperId",
                table: "AppQuestionPaperBlueprints",
                column: "QuestionPaperId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppQuestionPaperQuestions_QuestionBankItemId",
                table: "AppQuestionPaperQuestions",
                column: "QuestionBankItemId");

            migrationBuilder.CreateIndex(
                name: "IX_AppQuestionPaperQuestions_QuestionPaperId_QuestionPaperSect~",
                table: "AppQuestionPaperQuestions",
                columns: new[] { "QuestionPaperId", "QuestionPaperSectionId", "QuestionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppQuestionPapers_AcademicYearId",
                table: "AppQuestionPapers",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_AppQuestionPapers_ExamId",
                table: "AppQuestionPapers",
                column: "ExamId");

            migrationBuilder.CreateIndex(
                name: "IX_AppQuestionPapers_ExamScheduleId",
                table: "AppQuestionPapers",
                column: "ExamScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_AppQuestionPapers_StandardId",
                table: "AppQuestionPapers",
                column: "StandardId");

            migrationBuilder.CreateIndex(
                name: "IX_AppQuestionPapers_Status",
                table: "AppQuestionPapers",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AppQuestionPapers_SubjectId",
                table: "AppQuestionPapers",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_AppQuestionPaperSections_QuestionPaperId_SectionName",
                table: "AppQuestionPaperSections",
                columns: new[] { "QuestionPaperId", "SectionName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppQuestionPaperSettings_TenantId_AcademicYearId",
                table: "AppQuestionPaperSettings",
                columns: new[] { "TenantId", "AcademicYearId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppQuestionTags_QuestionBankItemId_TagName",
                table: "AppQuestionTags",
                columns: new[] { "QuestionBankItemId", "TagName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppQuestionUsageLogs_QuestionBankItemId",
                table: "AppQuestionUsageLogs",
                column: "QuestionBankItemId");

            migrationBuilder.CreateIndex(
                name: "IX_AppQuestionUsageLogs_Source_SourceEntityId",
                table: "AppQuestionUsageLogs",
                columns: new[] { "Source", "SourceEntityId" });


        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppQuestionAttachments");

            migrationBuilder.DropTable(
                name: "AppQuestionBankSettings");

            migrationBuilder.DropTable(
                name: "AppQuestionOptions");

            migrationBuilder.DropTable(
                name: "AppQuestionPaperBlueprintBloomRules");

            migrationBuilder.DropTable(
                name: "AppQuestionPaperBlueprintChapterRules");

            migrationBuilder.DropTable(
                name: "AppQuestionPaperBlueprintDifficultyRules");

            migrationBuilder.DropTable(
                name: "AppQuestionPaperQuestions");

            migrationBuilder.DropTable(
                name: "AppQuestionPaperSections");

            migrationBuilder.DropTable(
                name: "AppQuestionPaperSettings");

            migrationBuilder.DropTable(
                name: "AppQuestionTags");

            migrationBuilder.DropTable(
                name: "AppQuestionUsageLogs");



            migrationBuilder.DropTable(
                name: "AppQuestionPaperBlueprints");

            migrationBuilder.DropTable(
                name: "AppQuestionPapers");

            migrationBuilder.DropTable(
                name: "AppQuestionBankItems");
        }
    }
}
