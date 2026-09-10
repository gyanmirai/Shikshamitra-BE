using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class SeparateAdmissionFinalizationFromStudentOnboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GrNumber",
                table: "AppStudents",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OnboardingCaseId",
                table: "AppStudentEnrollments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RollNumber",
                table: "AppStudentEnrollments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FinalizedAt",
                table: "AppAdmissionApplications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OnboardingCaseId",
                table: "AppAdmissionApplications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StudentId",
                table: "AppAdmissionApplications",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppStudentIdentifierSeries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    StandardId = table.Column<Guid>(type: "uuid", nullable: true),
                    IdentifierType = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Prefix = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Suffix = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Padding = table.Column<int>(type: "integer", nullable: false),
                    NextSequence = table.Column<long>(type: "bigint", nullable: false),
                    MaximumSequence = table.Column<long>(type: "bigint", nullable: true),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EffectiveUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_AppStudentIdentifierSeries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppStudentOnboardingCases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicantId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    OfferingId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    IntendedBoardId = table.Column<Guid>(type: "uuid", nullable: false),
                    IntendedMediumId = table.Column<Guid>(type: "uuid", nullable: false),
                    IntendedStageId = table.Column<Guid>(type: "uuid", nullable: false),
                    IntendedStandardId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ActivationIdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    AssignedToUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    GuardianContactVerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DivisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    SeatReservationId = table.Column<Guid>(type: "uuid", nullable: true),
                    IdentifierAllocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    RollNumber = table.Column<int>(type: "integer", nullable: true),
                    StudentEnrollmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReadyAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_AppStudentOnboardingCases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppStudentOnboardingCases_AppAcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AppAcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppStudentOnboardingCases_AppAdmissionApplicants_ApplicantId",
                        column: x => x.ApplicantId,
                        principalTable: "AppAdmissionApplicants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppStudentOnboardingCases_AppAdmissionApplications_Applicat~",
                        column: x => x.ApplicationId,
                        principalTable: "AppAdmissionApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppStudentOnboardingCases_AppAdmissionOfferings_OfferingId",
                        column: x => x.OfferingId,
                        principalTable: "AppAdmissionOfferings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppStudentOnboardingCases_AppEducationBoards_IntendedBoardId",
                        column: x => x.IntendedBoardId,
                        principalTable: "AppEducationBoards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppStudentOnboardingCases_AppEducationMediumStages_Intended~",
                        column: x => x.IntendedStageId,
                        principalTable: "AppEducationMediumStages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppStudentOnboardingCases_AppEducationMediums_IntendedMediu~",
                        column: x => x.IntendedMediumId,
                        principalTable: "AppEducationMediums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppStudentOnboardingCases_AppStandards_IntendedStandardId",
                        column: x => x.IntendedStandardId,
                        principalTable: "AppStandards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppStudentOnboardingCases_AppStudents_StudentId",
                        column: x => x.StudentId,
                        principalTable: "AppStudents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppStudentOnboardingPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    OfferingId = table.Column<Guid>(type: "uuid", nullable: true),
                    StandardId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    RequireDocuments = table.Column<bool>(type: "boolean", nullable: false),
                    RequireFamilyDetails = table.Column<bool>(type: "boolean", nullable: false),
                    RequireCommunicationDetails = table.Column<bool>(type: "boolean", nullable: false),
                    RequireMedicalDetails = table.Column<bool>(type: "boolean", nullable: false),
                    RequireFeeClearance = table.Column<bool>(type: "boolean", nullable: false),
                    RequireStudentAccount = table.Column<bool>(type: "boolean", nullable: false),
                    RequireGuardianAccount = table.Column<bool>(type: "boolean", nullable: false),
                    RequireServiceConsent = table.Column<bool>(type: "boolean", nullable: false),
                    SeatReservationMinutes = table.Column<int>(type: "integer", nullable: false),
                    SlaHours = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_AppStudentOnboardingPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppStudentIdentifierAllocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    OnboardingCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    NumberSeriesId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdentifierType = table.Column<int>(type: "integer", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    Value = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IsManual = table.Column<bool>(type: "boolean", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    AllocatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CommittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_AppStudentIdentifierAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppStudentIdentifierAllocations_AppStudentIdentifierSeries_~",
                        column: x => x.NumberSeriesId,
                        principalTable: "AppStudentIdentifierSeries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppStudentIdentifierAllocations_AppStudentOnboardingCases_O~",
                        column: x => x.OnboardingCaseId,
                        principalTable: "AppStudentOnboardingCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppStudentIdentifierAllocations_AppStudents_StudentId",
                        column: x => x.StudentId,
                        principalTable: "AppStudents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppStudentOnboardingEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    OnboardingCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    MetadataJson = table.Column<string>(type: "text", nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppStudentOnboardingEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppStudentOnboardingEvents_AppStudentOnboardingCases_Onboar~",
                        column: x => x.OnboardingCaseId,
                        principalTable: "AppStudentOnboardingCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppStudentOnboardingServiceHandoffs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    OnboardingCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Required = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    LastAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AcknowledgedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_AppStudentOnboardingServiceHandoffs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppStudentOnboardingServiceHandoffs_AppStudentOnboardingCas~",
                        column: x => x.OnboardingCaseId,
                        principalTable: "AppStudentOnboardingCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppStudentOnboardingSteps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    OnboardingCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Label = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    Required = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Detail = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CompletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WaiverReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_AppStudentOnboardingSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppStudentOnboardingSteps_AppStudentOnboardingCases_Onboard~",
                        column: x => x.OnboardingCaseId,
                        principalTable: "AppStudentOnboardingCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppStudentSeatReservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    OnboardingCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    StandardId = table.Column<Guid>(type: "uuid", nullable: false),
                    DivisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeatNumber = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ReservedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CommittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReleasedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReleaseReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_AppStudentSeatReservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppStudentSeatReservations_AppDivisions_DivisionId",
                        column: x => x.DivisionId,
                        principalTable: "AppDivisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppStudentSeatReservations_AppStudentOnboardingCases_Onboar~",
                        column: x => x.OnboardingCaseId,
                        principalTable: "AppStudentOnboardingCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppStudents_TenantId_GrNumber",
                table: "AppStudents",
                columns: new[] { "TenantId", "GrNumber" },
                unique: true,
                filter: "\"GrNumber\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentEnrollments_OnboardingCaseId",
                table: "AppStudentEnrollments",
                column: "OnboardingCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentEnrollments_TenantId_AcademicYearId_DivisionId_Ro~",
                table: "AppStudentEnrollments",
                columns: new[] { "TenantId", "AcademicYearId", "DivisionId", "RollNumber" },
                unique: true,
                filter: "\"IsActive\" = true AND \"DivisionId\" IS NOT NULL AND \"RollNumber\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentEnrollments_TenantId_OnboardingCaseId",
                table: "AppStudentEnrollments",
                columns: new[] { "TenantId", "OnboardingCaseId" },
                unique: true,
                filter: "\"OnboardingCaseId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionApplications_OnboardingCaseId",
                table: "AppAdmissionApplications",
                column: "OnboardingCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionApplications_StudentId",
                table: "AppAdmissionApplications",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionApplications_TenantId_OnboardingCaseId",
                table: "AppAdmissionApplications",
                columns: new[] { "TenantId", "OnboardingCaseId" },
                unique: true,
                filter: "\"OnboardingCaseId\" IS NOT NULL AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentIdentifierAllocations_NumberSeriesId",
                table: "AppStudentIdentifierAllocations",
                column: "NumberSeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentIdentifierAllocations_OnboardingCaseId",
                table: "AppStudentIdentifierAllocations",
                column: "OnboardingCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentIdentifierAllocations_StudentId",
                table: "AppStudentIdentifierAllocations",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentIdentifierAllocations_TenantId_IdentifierType_Val~",
                table: "AppStudentIdentifierAllocations",
                columns: new[] { "TenantId", "IdentifierType", "Value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentIdentifierAllocations_TenantId_OnboardingCaseId_I~",
                table: "AppStudentIdentifierAllocations",
                columns: new[] { "TenantId", "OnboardingCaseId", "IdentifierType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentIdentifierSeries_TenantId_AcademicYearId_Standard~",
                table: "AppStudentIdentifierSeries",
                columns: new[] { "TenantId", "AcademicYearId", "StandardId", "IdentifierType", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentIdentifierSeries_TenantId_Code",
                table: "AppStudentIdentifierSeries",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentOnboardingCases_AcademicYearId",
                table: "AppStudentOnboardingCases",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentOnboardingCases_ApplicantId",
                table: "AppStudentOnboardingCases",
                column: "ApplicantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentOnboardingCases_ApplicationId",
                table: "AppStudentOnboardingCases",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentOnboardingCases_IntendedBoardId",
                table: "AppStudentOnboardingCases",
                column: "IntendedBoardId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentOnboardingCases_IntendedMediumId",
                table: "AppStudentOnboardingCases",
                column: "IntendedMediumId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentOnboardingCases_IntendedStageId",
                table: "AppStudentOnboardingCases",
                column: "IntendedStageId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentOnboardingCases_IntendedStandardId",
                table: "AppStudentOnboardingCases",
                column: "IntendedStandardId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentOnboardingCases_OfferingId",
                table: "AppStudentOnboardingCases",
                column: "OfferingId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentOnboardingCases_StudentId",
                table: "AppStudentOnboardingCases",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentOnboardingCases_TenantId_AcademicYearId_Status",
                table: "AppStudentOnboardingCases",
                columns: new[] { "TenantId", "AcademicYearId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentOnboardingCases_TenantId_ActivationIdempotencyKey",
                table: "AppStudentOnboardingCases",
                columns: new[] { "TenantId", "ActivationIdempotencyKey" },
                unique: true,
                filter: "\"ActivationIdempotencyKey\" IS NOT NULL AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentOnboardingCases_TenantId_ApplicationId",
                table: "AppStudentOnboardingCases",
                columns: new[] { "TenantId", "ApplicationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentOnboardingCases_TenantId_IdempotencyKey",
                table: "AppStudentOnboardingCases",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentOnboardingCases_TenantId_StudentId_AcademicYearId",
                table: "AppStudentOnboardingCases",
                columns: new[] { "TenantId", "StudentId", "AcademicYearId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentOnboardingEvents_OnboardingCaseId",
                table: "AppStudentOnboardingEvents",
                column: "OnboardingCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentOnboardingEvents_TenantId_OnboardingCaseId_Creati~",
                table: "AppStudentOnboardingEvents",
                columns: new[] { "TenantId", "OnboardingCaseId", "CreationTime" });

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentOnboardingPolicies_TenantId_AcademicYearId_IsActi~",
                table: "AppStudentOnboardingPolicies",
                columns: new[] { "TenantId", "AcademicYearId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentOnboardingPolicies_TenantId_AcademicYearId_Offeri~",
                table: "AppStudentOnboardingPolicies",
                columns: new[] { "TenantId", "AcademicYearId", "OfferingId", "StandardId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentOnboardingServiceHandoffs_OnboardingCaseId",
                table: "AppStudentOnboardingServiceHandoffs",
                column: "OnboardingCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentOnboardingServiceHandoffs_TenantId_IdempotencyKey",
                table: "AppStudentOnboardingServiceHandoffs",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentOnboardingServiceHandoffs_TenantId_OnboardingCase~",
                table: "AppStudentOnboardingServiceHandoffs",
                columns: new[] { "TenantId", "OnboardingCaseId", "ServiceKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentOnboardingServiceHandoffs_TenantId_Status_LastAtt~",
                table: "AppStudentOnboardingServiceHandoffs",
                columns: new[] { "TenantId", "Status", "LastAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentOnboardingSteps_OnboardingCaseId",
                table: "AppStudentOnboardingSteps",
                column: "OnboardingCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentOnboardingSteps_TenantId_OnboardingCaseId_Key",
                table: "AppStudentOnboardingSteps",
                columns: new[] { "TenantId", "OnboardingCaseId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentSeatReservations_DivisionId",
                table: "AppStudentSeatReservations",
                column: "DivisionId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentSeatReservations_OnboardingCaseId",
                table: "AppStudentSeatReservations",
                column: "OnboardingCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentSeatReservations_TenantId_AcademicYearId_Divisio~1",
                table: "AppStudentSeatReservations",
                columns: new[] { "TenantId", "AcademicYearId", "DivisionId", "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentSeatReservations_TenantId_AcademicYearId_Division~",
                table: "AppStudentSeatReservations",
                columns: new[] { "TenantId", "AcademicYearId", "DivisionId", "SeatNumber" },
                unique: true,
                filter: "\"Status\" = 0 AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentSeatReservations_TenantId_OnboardingCaseId_Status",
                table: "AppStudentSeatReservations",
                columns: new[] { "TenantId", "OnboardingCaseId", "Status" });

            migrationBuilder.AddForeignKey(
                name: "FK_AppAdmissionApplications_AppStudentOnboardingCases_Onboardi~",
                table: "AppAdmissionApplications",
                column: "OnboardingCaseId",
                principalTable: "AppStudentOnboardingCases",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AppAdmissionApplications_AppStudents_StudentId",
                table: "AppAdmissionApplications",
                column: "StudentId",
                principalTable: "AppStudents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AppStudentEnrollments_AppStudentOnboardingCases_OnboardingC~",
                table: "AppStudentEnrollments",
                column: "OnboardingCaseId",
                principalTable: "AppStudentOnboardingCases",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppAdmissionApplications_AppStudentOnboardingCases_Onboardi~",
                table: "AppAdmissionApplications");

            migrationBuilder.DropForeignKey(
                name: "FK_AppAdmissionApplications_AppStudents_StudentId",
                table: "AppAdmissionApplications");

            migrationBuilder.DropForeignKey(
                name: "FK_AppStudentEnrollments_AppStudentOnboardingCases_OnboardingC~",
                table: "AppStudentEnrollments");

            migrationBuilder.DropTable(
                name: "AppStudentIdentifierAllocations");

            migrationBuilder.DropTable(
                name: "AppStudentOnboardingEvents");

            migrationBuilder.DropTable(
                name: "AppStudentOnboardingPolicies");

            migrationBuilder.DropTable(
                name: "AppStudentOnboardingServiceHandoffs");

            migrationBuilder.DropTable(
                name: "AppStudentOnboardingSteps");

            migrationBuilder.DropTable(
                name: "AppStudentSeatReservations");

            migrationBuilder.DropTable(
                name: "AppStudentIdentifierSeries");

            migrationBuilder.DropTable(
                name: "AppStudentOnboardingCases");

            migrationBuilder.DropIndex(
                name: "IX_AppStudents_TenantId_GrNumber",
                table: "AppStudents");

            migrationBuilder.DropIndex(
                name: "IX_AppStudentEnrollments_OnboardingCaseId",
                table: "AppStudentEnrollments");

            migrationBuilder.DropIndex(
                name: "IX_AppStudentEnrollments_TenantId_AcademicYearId_DivisionId_Ro~",
                table: "AppStudentEnrollments");

            migrationBuilder.DropIndex(
                name: "IX_AppStudentEnrollments_TenantId_OnboardingCaseId",
                table: "AppStudentEnrollments");

            migrationBuilder.DropIndex(
                name: "IX_AppAdmissionApplications_OnboardingCaseId",
                table: "AppAdmissionApplications");

            migrationBuilder.DropIndex(
                name: "IX_AppAdmissionApplications_StudentId",
                table: "AppAdmissionApplications");

            migrationBuilder.DropIndex(
                name: "IX_AppAdmissionApplications_TenantId_OnboardingCaseId",
                table: "AppAdmissionApplications");

            migrationBuilder.DropColumn(
                name: "GrNumber",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "OnboardingCaseId",
                table: "AppStudentEnrollments");

            migrationBuilder.DropColumn(
                name: "RollNumber",
                table: "AppStudentEnrollments");

            migrationBuilder.DropColumn(
                name: "FinalizedAt",
                table: "AppAdmissionApplications");

            migrationBuilder.DropColumn(
                name: "OnboardingCaseId",
                table: "AppAdmissionApplications");

            migrationBuilder.DropColumn(
                name: "StudentId",
                table: "AppAdmissionApplications");
        }
    }
}
