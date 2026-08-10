using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddAdmissionCrmEnrollmentReadiness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FamilyAccessGuardianPersonId",
                table: "AppAdmissionApplicantStatusAccesses",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppAdmissionCrmEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    EventType = table.Column<int>(type: "integer", nullable: false),
                    StartsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Location = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Instructions = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AllowFamilySelfScheduling = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_AppAdmissionCrmEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppAdmissionCrmReviewConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    RequiredCompletedReviews = table.Column<int>(type: "integer", nullable: false),
                    RequireConflictDeclaration = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_AppAdmissionCrmReviewConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppAdmissionCrmStructuredReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeadId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConfigurationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConfigurationVersion = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    RequiredCompletedReviews = table.Column<int>(type: "integer", nullable: false),
                    RequireConflictDeclaration = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReadyAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClosureReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_AppAdmissionCrmStructuredReviews", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppAdmissionCrmEventRegistrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeadId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ReservedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CheckedInAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NoShowRecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_AppAdmissionCrmEventRegistrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppAdmissionCrmEventRegistrations_AppAdmissionCrmEvents_Eve~",
                        column: x => x.EventId,
                        principalTable: "AppAdmissionCrmEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppAdmissionCrmReviewConfigurationCriteria",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConfigurationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Label = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    MaximumScore = table.Column<int>(type: "integer", nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_AppAdmissionCrmReviewConfigurationCriteria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppAdmissionCrmReviewConfigurationCriteria_AppAdmissionCrmR~",
                        column: x => x.ConfigurationId,
                        principalTable: "AppAdmissionCrmReviewConfigurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppAdmissionCrmReviewAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DueAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    HasDeclaredConflict = table.Column<bool>(type: "boolean", nullable: true),
                    ConflictReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ConflictDeclaredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Recommendation = table.Column<int>(type: "integer", nullable: true),
                    ConfidentialRecommendation = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ScoresJson = table.Column<string>(type: "jsonb", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_AppAdmissionCrmReviewAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppAdmissionCrmReviewAssignments_AppAdmissionCrmStructuredR~",
                        column: x => x.ReviewId,
                        principalTable: "AppAdmissionCrmStructuredReviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppAdmissionCrmStructuredReviewCriteria",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Label = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    MaximumScore = table.Column<int>(type: "integer", nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_AppAdmissionCrmStructuredReviewCriteria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppAdmissionCrmStructuredReviewCriteria_AppAdmissionCrmStru~",
                        column: x => x.ReviewId,
                        principalTable: "AppAdmissionCrmStructuredReviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionApplicantStatusAccesses_TenantId_FamilyAccessGu~",
                table: "AppAdmissionApplicantStatusAccesses",
                columns: new[] { "TenantId", "FamilyAccessGuardianPersonId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionCrmEventRegistrations_EventId",
                table: "AppAdmissionCrmEventRegistrations",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionCrmEventRegistrations_TenantId_EventId_Status",
                table: "AppAdmissionCrmEventRegistrations",
                columns: new[] { "TenantId", "EventId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionCrmEventRegistrations_TenantId_LeadId_Status",
                table: "AppAdmissionCrmEventRegistrations",
                columns: new[] { "TenantId", "LeadId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionCrmEvents_TenantId_AcademicYearId_StartsAt",
                table: "AppAdmissionCrmEvents",
                columns: new[] { "TenantId", "AcademicYearId", "StartsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionCrmEvents_TenantId_Status_StartsAt",
                table: "AppAdmissionCrmEvents",
                columns: new[] { "TenantId", "Status", "StartsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionCrmReviewAssignments_ReviewId",
                table: "AppAdmissionCrmReviewAssignments",
                column: "ReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionCrmReviewAssignments_TenantId_ReviewerUserId_St~",
                table: "AppAdmissionCrmReviewAssignments",
                columns: new[] { "TenantId", "ReviewerUserId", "Status", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionCrmReviewAssignments_TenantId_ReviewId_Reviewer~",
                table: "AppAdmissionCrmReviewAssignments",
                columns: new[] { "TenantId", "ReviewId", "ReviewerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionCrmReviewConfigurationCriteria_ConfigurationId",
                table: "AppAdmissionCrmReviewConfigurationCriteria",
                column: "ConfigurationId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionCrmReviewConfigurationCriteria_TenantId_Configu~",
                table: "AppAdmissionCrmReviewConfigurationCriteria",
                columns: new[] { "TenantId", "ConfigurationId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionCrmReviewConfigurations_TenantId_AcademicYearId",
                table: "AppAdmissionCrmReviewConfigurations",
                columns: new[] { "TenantId", "AcademicYearId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionCrmStructuredReviewCriteria_ReviewId",
                table: "AppAdmissionCrmStructuredReviewCriteria",
                column: "ReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionCrmStructuredReviewCriteria_TenantId_ReviewId_K~",
                table: "AppAdmissionCrmStructuredReviewCriteria",
                columns: new[] { "TenantId", "ReviewId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionCrmStructuredReviews_TenantId_AcademicYearId_St~",
                table: "AppAdmissionCrmStructuredReviews",
                columns: new[] { "TenantId", "AcademicYearId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionCrmStructuredReviews_TenantId_LeadId",
                table: "AppAdmissionCrmStructuredReviews",
                columns: new[] { "TenantId", "LeadId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppAdmissionCrmEventRegistrations");

            migrationBuilder.DropTable(
                name: "AppAdmissionCrmReviewAssignments");

            migrationBuilder.DropTable(
                name: "AppAdmissionCrmReviewConfigurationCriteria");

            migrationBuilder.DropTable(
                name: "AppAdmissionCrmStructuredReviewCriteria");

            migrationBuilder.DropTable(
                name: "AppAdmissionCrmEvents");

            migrationBuilder.DropTable(
                name: "AppAdmissionCrmReviewConfigurations");

            migrationBuilder.DropTable(
                name: "AppAdmissionCrmStructuredReviews");

            migrationBuilder.DropIndex(
                name: "IX_AppAdmissionApplicantStatusAccesses_TenantId_FamilyAccessGu~",
                table: "AppAdmissionApplicantStatusAccesses");

            migrationBuilder.DropColumn(
                name: "FamilyAccessGuardianPersonId",
                table: "AppAdmissionApplicantStatusAccesses");
        }
    }
}
