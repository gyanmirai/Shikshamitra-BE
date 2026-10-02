using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentRecordGovernanceAndOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "CertificateNumber",
                table: "AppStudentCertificates",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            migrationBuilder.AddColumn<Guid>(
                name: "AcademicYearId",
                table: "AppStudentCertificates",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "AppStudentCertificates",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceCertificateId",
                table: "AppStudentCertificates",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TransitionId",
                table: "AppStudentCertificates",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppStudentGuardianAccessReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangeRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousLinksJson = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OnApprovedChangeMode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ResolutionJson = table.Column<string>(type: "jsonb", nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_AppStudentGuardianAccessReviews", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppStudentIdCards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: true),
                    CardNumber = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StudentSnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    TemplateSnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IssuedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_AppStudentIdCards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppStudentIdCards_AppStudents_StudentId",
                        column: x => x.StudentId,
                        principalTable: "AppStudents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppStudentIdCardTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConfigurationJson = table.Column<string>(type: "jsonb", nullable: false),
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
                    table.PrimaryKey("PK_AppStudentIdCardTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppStudentRecordChanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CorrectsChangeId = table.Column<Guid>(type: "uuid", nullable: true),
                    PolicyVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Section = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Action = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ClientKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    BeforeJson = table.Column<string>(type: "jsonb", nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    PolicyJson = table.Column<string>(type: "jsonb", nullable: false),
                    ApprovalStagesJson = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ApprovalCount = table.Column<int>(type: "integer", nullable: false),
                    RequiredApprovals = table.Column<int>(type: "integer", nullable: false),
                    EffectiveAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AppliedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExceptionCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
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
                    table.PrimaryKey("PK_AppStudentRecordChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppStudentRecordChanges_AppStudents_StudentId",
                        column: x => x.StudentId,
                        principalTable: "AppStudents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppStudentRecordDocumentEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: true),
                    DocumentType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppStudentRecordDocumentEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppStudentRecordDuplicateReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    OtherStudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ReviewReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
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
                    table.PrimaryKey("PK_AppStudentRecordDuplicateReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppStudentRecordDuplicateReviews_AppStudents_OtherStudentId",
                        column: x => x.OtherStudentId,
                        principalTable: "AppStudents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppStudentRecordDuplicateReviews_AppStudents_StudentId",
                        column: x => x.StudentId,
                        principalTable: "AppStudents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppStudentRecordLegacyOverrides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DecidedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    EvidenceReference = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DecisionRemarks = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_AppStudentRecordLegacyOverrides", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppStudentRecordPhotoDrafts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    BlobName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppStudentRecordPhotoDrafts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppStudentRecordPhotoDrafts_AppStudents_StudentId",
                        column: x => x.StudentId,
                        principalTable: "AppStudents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppStudentRecordRenewalCases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    StandardId = table.Column<Guid>(type: "uuid", nullable: false),
                    DivisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RollNumber = table.Column<int>(type: "integer", nullable: true),
                    TransitionId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentEnrollmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PolicyJson = table.Column<string>(type: "text", nullable: false),
                    OnboardingRulesJson = table.Column<string>(type: "text", nullable: false),
                    GuardianReviewConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    GuardianReviewedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    GuardianReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewRemarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SourceSnapshotJson = table.Column<string>(type: "text", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
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
                    table.PrimaryKey("PK_AppStudentRecordRenewalCases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppStudentRecordRenewalCases_AppStudents_StudentId",
                        column: x => x.StudentId,
                        principalTable: "AppStudents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppStudentRecordReportJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ReportType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    RequestJson = table.Column<string>(type: "jsonb", nullable: false),
                    AuthorityHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BlobName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ErrorCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LeaseUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    GenerationId = table.Column<Guid>(type: "uuid", nullable: true),
                    RowCount = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_AppStudentRecordReportJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppStudentRecordReturnHandoffs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdmissionApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Route = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RequestJson = table.Column<string>(type: "text", nullable: false),
                    PolicyJson = table.Column<string>(type: "text", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppStudentRecordReturnHandoffs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppStudentRecordsConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: true),
                    DraftPolicyJson = table.Column<string>(type: "text", nullable: false),
                    PublishedVersionId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_AppStudentRecordsConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppStudentRecordTransitionBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RequestJson = table.Column<string>(type: "text", nullable: false),
                    ExceptionRowsJson = table.Column<string>(type: "text", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppStudentRecordTransitionBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppStudentRecordChangeDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ChangeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Stage = table.Column<int>(type: "integer", nullable: false),
                    Decision = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppStudentRecordChangeDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppStudentRecordChangeDecisions_AppStudentRecordChanges_Cha~",
                        column: x => x.ChangeId,
                        principalTable: "AppStudentRecordChanges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppStudentRecordsPolicyVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConfigurationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: true),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    PolicyJson = table.Column<string>(type: "text", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppStudentRecordsPolicyVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppStudentRecordsPolicyVersions_AppStudentRecordsConfigurat~",
                        column: x => x.ConfigurationId,
                        principalTable: "AppStudentRecordsConfigurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppStudentRecordTransitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceEnrollmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Outcome = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    InputJson = table.Column<string>(type: "text", nullable: false),
                    BeforeJson = table.Column<string>(type: "text", nullable: false),
                    AfterJson = table.Column<string>(type: "text", nullable: true),
                    PolicyVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    PolicyJson = table.Column<string>(type: "text", nullable: false),
                    ApprovalRuleJson = table.Column<string>(type: "text", nullable: false),
                    RequestedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovalStageCount = table.Column<int>(type: "integer", nullable: false),
                    CurrentApprovalStage = table.Column<int>(type: "integer", nullable: false),
                    LastApprovedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RenewalCaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetEnrollmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CorrectionOfId = table.Column<Guid>(type: "uuid", nullable: true),
                    AppliedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
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
                    table.PrimaryKey("PK_AppStudentRecordTransitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppStudentRecordTransitions_AppStudentEnrollments_SourceEnr~",
                        column: x => x.SourceEnrollmentId,
                        principalTable: "AppStudentEnrollments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppStudentRecordTransitions_AppStudentRecordTransitionBatch~",
                        column: x => x.BatchId,
                        principalTable: "AppStudentRecordTransitionBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppStudentRecordTransitions_AppStudents_StudentId",
                        column: x => x.StudentId,
                        principalTable: "AppStudents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppStudentRecordTransitionEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    TransitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Stage = table.Column<int>(type: "integer", nullable: false),
                    Remarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppStudentRecordTransitionEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppStudentRecordTransitionEvents_AppStudentRecordTransition~",
                        column: x => x.TransitionId,
                        principalTable: "AppStudentRecordTransitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentCertificates_TenantId_IdempotencyKey",
                table: "AppStudentCertificates",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentGuardianAccessReviews_TenantId_ChangeRequestId",
                table: "AppStudentGuardianAccessReviews",
                columns: new[] { "TenantId", "ChangeRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentGuardianAccessReviews_TenantId_StudentId_Creation~",
                table: "AppStudentGuardianAccessReviews",
                columns: new[] { "TenantId", "StudentId", "CreationTime" });

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentIdCards_StudentId",
                table: "AppStudentIdCards",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentIdCards_TenantId_CardNumber",
                table: "AppStudentIdCards",
                columns: new[] { "TenantId", "CardNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentIdCards_TenantId_IdempotencyKey",
                table: "AppStudentIdCards",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentIdCards_TenantId_StudentId_CreationTime",
                table: "AppStudentIdCards",
                columns: new[] { "TenantId", "StudentId", "CreationTime" });

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentIdCardTemplates_TenantId",
                table: "AppStudentIdCardTemplates",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentRecordChangeDecisions_ChangeId_Stage",
                table: "AppStudentRecordChangeDecisions",
                columns: new[] { "ChangeId", "Stage" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentRecordChanges_StudentId",
                table: "AppStudentRecordChanges",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentRecordChanges_TenantId_Status_EffectiveAt",
                table: "AppStudentRecordChanges",
                columns: new[] { "TenantId", "Status", "EffectiveAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentRecordChanges_TenantId_StudentId_ClientKey",
                table: "AppStudentRecordChanges",
                columns: new[] { "TenantId", "StudentId", "ClientKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentRecordDocumentEvents_TenantId_DocumentId_Creation~",
                table: "AppStudentRecordDocumentEvents",
                columns: new[] { "TenantId", "DocumentId", "CreationTime" });

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentRecordDocumentEvents_TenantId_StudentId_CreationT~",
                table: "AppStudentRecordDocumentEvents",
                columns: new[] { "TenantId", "StudentId", "CreationTime" });

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentRecordDuplicateReviews_OtherStudentId",
                table: "AppStudentRecordDuplicateReviews",
                column: "OtherStudentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentRecordDuplicateReviews_StudentId",
                table: "AppStudentRecordDuplicateReviews",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentRecordDuplicateReviews_TenantId_StudentId_OtherSt~",
                table: "AppStudentRecordDuplicateReviews",
                columns: new[] { "TenantId", "StudentId", "OtherStudentId" },
                unique: true,
                filter: "\"Status\" IN ('Open', 'ConfirmedDuplicate') AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentRecordLegacyOverrides_TenantId_StudentId_Status",
                table: "AppStudentRecordLegacyOverrides",
                columns: new[] { "TenantId", "StudentId", "Status" },
                unique: true,
                filter: "\"Status\" IN ('Pending', 'Approved') AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentRecordPhotoDrafts_StudentId",
                table: "AppStudentRecordPhotoDrafts",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentRecordPhotoDrafts_TenantId_StudentId_UploadedBy",
                table: "AppStudentRecordPhotoDrafts",
                columns: new[] { "TenantId", "StudentId", "UploadedBy" });

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentRecordRenewalCases_StudentId",
                table: "AppStudentRecordRenewalCases",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentRecordRenewalCases_TenantId_IdempotencyKey",
                table: "AppStudentRecordRenewalCases",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentRecordRenewalCases_TenantId_StudentId_AcademicYea~",
                table: "AppStudentRecordRenewalCases",
                columns: new[] { "TenantId", "StudentId", "AcademicYearId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentRecordReportJobs_Status_LeaseUntil",
                table: "AppStudentRecordReportJobs",
                columns: new[] { "Status", "LeaseUntil" });

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentRecordReportJobs_TenantId_RequestedBy_ClientKey",
                table: "AppStudentRecordReportJobs",
                columns: new[] { "TenantId", "RequestedBy", "ClientKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentRecordReturnHandoffs_TenantId_IdempotencyKey",
                table: "AppStudentRecordReturnHandoffs",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentRecordReturnHandoffs_TenantId_StudentId_AcademicY~",
                table: "AppStudentRecordReturnHandoffs",
                columns: new[] { "TenantId", "StudentId", "AcademicYearId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentRecordsConfigurations_TenantId",
                table: "AppStudentRecordsConfigurations",
                column: "TenantId",
                unique: true,
                filter: "\"AcademicYearId\" IS NULL AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentRecordsConfigurations_TenantId_AcademicYearId",
                table: "AppStudentRecordsConfigurations",
                columns: new[] { "TenantId", "AcademicYearId" },
                unique: true,
                filter: "\"AcademicYearId\" IS NOT NULL AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentRecordsPolicyVersions_ConfigurationId",
                table: "AppStudentRecordsPolicyVersions",
                column: "ConfigurationId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentRecordsPolicyVersions_TenantId_ConfigurationId_Ve~",
                table: "AppStudentRecordsPolicyVersions",
                columns: new[] { "TenantId", "ConfigurationId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentRecordTransitionBatches_TenantId_IdempotencyKey",
                table: "AppStudentRecordTransitionBatches",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentRecordTransitionEvents_TenantId_TransitionId_Crea~",
                table: "AppStudentRecordTransitionEvents",
                columns: new[] { "TenantId", "TransitionId", "CreationTime" });

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentRecordTransitionEvents_TransitionId",
                table: "AppStudentRecordTransitionEvents",
                column: "TransitionId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentRecordTransitions_BatchId",
                table: "AppStudentRecordTransitions",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentRecordTransitions_SourceEnrollmentId",
                table: "AppStudentRecordTransitions",
                column: "SourceEnrollmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentRecordTransitions_StudentId",
                table: "AppStudentRecordTransitions",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentRecordTransitions_TenantId_BatchId_StudentId",
                table: "AppStudentRecordTransitions",
                columns: new[] { "TenantId", "BatchId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentRecordTransitions_TenantId_CorrectionOfId",
                table: "AppStudentRecordTransitions",
                columns: new[] { "TenantId", "CorrectionOfId" },
                unique: true,
                filter: "\"CorrectionOfId\" IS NOT NULL AND \"IsDeleted\" = false AND \"Status\" NOT IN ('Rejected','Cancelled')");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentRecordTransitions_TenantId_Status_EffectiveDate",
                table: "AppStudentRecordTransitions",
                columns: new[] { "TenantId", "Status", "EffectiveDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentRecordTransitions_TenantId_StudentId_AcademicYear~",
                table: "AppStudentRecordTransitions",
                columns: new[] { "TenantId", "StudentId", "AcademicYearId", "CreationTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Once used, these tables are permanent governance evidence. Roll forward instead.
            migrationBuilder.Sql("""
                DO $records_rollback$
                DECLARE records_table text; has_evidence boolean;
                BEGIN
                    FOREACH records_table IN ARRAY ARRAY[
                        'AppStudentGuardianAccessReviews', 'AppStudentIdCards', 'AppStudentIdCardTemplates',
                        'AppStudentRecordChangeDecisions', 'AppStudentRecordDocumentEvents',
                        'AppStudentRecordDuplicateReviews', 'AppStudentRecordLegacyOverrides',
                        'AppStudentRecordPhotoDrafts', 'AppStudentRecordRenewalCases',
                        'AppStudentRecordReportJobs', 'AppStudentRecordReturnHandoffs',
                        'AppStudentRecordsPolicyVersions', 'AppStudentRecordTransitionEvents',
                        'AppStudentRecordChanges', 'AppStudentRecordsConfigurations',
                        'AppStudentRecordTransitions', 'AppStudentRecordTransitionBatches']
                    LOOP
                        EXECUTE format('SELECT EXISTS (SELECT 1 FROM %I)', records_table) INTO has_evidence;
                        IF has_evidence THEN
                            RAISE EXCEPTION 'Student Records rollback would remove evidence in %; use a forward fix.', records_table;
                        END IF;
                    END LOOP;
                    IF EXISTS (SELECT 1 FROM "AppStudentCertificates"
                        WHERE "AcademicYearId" IS NOT NULL OR "IdempotencyKey" IS NOT NULL
                           OR "SourceCertificateId" IS NOT NULL OR "TransitionId" IS NOT NULL
                           OR length("CertificateNumber") > 32) THEN
                        RAISE EXCEPTION 'Student Records rollback would remove certificate evidence; use a forward fix.';
                    END IF;
                END $records_rollback$;
                """);

            migrationBuilder.DropTable(
                name: "AppStudentGuardianAccessReviews");

            migrationBuilder.DropTable(
                name: "AppStudentIdCards");

            migrationBuilder.DropTable(
                name: "AppStudentIdCardTemplates");

            migrationBuilder.DropTable(
                name: "AppStudentRecordChangeDecisions");

            migrationBuilder.DropTable(
                name: "AppStudentRecordDocumentEvents");

            migrationBuilder.DropTable(
                name: "AppStudentRecordDuplicateReviews");

            migrationBuilder.DropTable(
                name: "AppStudentRecordLegacyOverrides");

            migrationBuilder.DropTable(
                name: "AppStudentRecordPhotoDrafts");

            migrationBuilder.DropTable(
                name: "AppStudentRecordRenewalCases");

            migrationBuilder.DropTable(
                name: "AppStudentRecordReportJobs");

            migrationBuilder.DropTable(
                name: "AppStudentRecordReturnHandoffs");

            migrationBuilder.DropTable(
                name: "AppStudentRecordsPolicyVersions");

            migrationBuilder.DropTable(
                name: "AppStudentRecordTransitionEvents");

            migrationBuilder.DropTable(
                name: "AppStudentRecordChanges");

            migrationBuilder.DropTable(
                name: "AppStudentRecordsConfigurations");

            migrationBuilder.DropTable(
                name: "AppStudentRecordTransitions");

            migrationBuilder.DropTable(
                name: "AppStudentRecordTransitionBatches");

            migrationBuilder.DropIndex(
                name: "IX_AppStudentCertificates_TenantId_IdempotencyKey",
                table: "AppStudentCertificates");

            migrationBuilder.DropColumn(
                name: "AcademicYearId",
                table: "AppStudentCertificates");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "AppStudentCertificates");

            migrationBuilder.DropColumn(
                name: "SourceCertificateId",
                table: "AppStudentCertificates");

            migrationBuilder.DropColumn(
                name: "TransitionId",
                table: "AppStudentCertificates");

            migrationBuilder.AlterColumn<string>(
                name: "CertificateNumber",
                table: "AppStudentCertificates",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(80)",
                oldMaxLength: 80);
        }
    }
}
