using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddCommunicationCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CommunicationCampaigns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicMediumId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceModule = table.Column<int>(type: "integer", nullable: false),
                    CampaignType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Subject = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Body = table.Column<string>(type: "text", nullable: false),
                    ImportanceLevel = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ApprovalRequired = table.Column<bool>(type: "boolean", nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    AnnouncementTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    AudienceDefinitionJson = table.Column<string>(type: "text", nullable: false),
                    ChannelsJson = table.Column<string>(type: "text", nullable: false),
                    FallbackOrderJson = table.Column<string>(type: "text", nullable: false),
                    AttachmentsJson = table.Column<string>(type: "text", nullable: false),
                    LinkUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    LanguageCode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ScheduledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RequiresAcknowledgement = table.Column<bool>(type: "boolean", nullable: false),
                    RecipientCount = table.Column<int>(type: "integer", nullable: false),
                    EligibleRecipientCount = table.Column<int>(type: "integer", nullable: false),
                    BlockedRecipientCount = table.Column<int>(type: "integer", nullable: false),
                    QueuedCount = table.Column<int>(type: "integer", nullable: false),
                    DeliveredCount = table.Column<int>(type: "integer", nullable: false),
                    FailedCount = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_CommunicationCampaigns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommunicationCampaigns_AnnouncementTypes_AnnouncementTypeId",
                        column: x => x.AnnouncementTypeId,
                        principalTable: "AnnouncementTypes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CommunicationChannelProviderSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Channel = table.Column<int>(type: "integer", nullable: false),
                    ProviderName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SenderId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    ConfigurationJson = table.Column<string>(type: "text", nullable: true),
                    SecretReference = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    FallbackOrderJson = table.Column<string>(type: "text", nullable: false),
                    DailyQuota = table.Column<int>(type: "integer", nullable: true),
                    MonthlyQuota = table.Column<int>(type: "integer", nullable: true),
                    ConsentRequired = table.Column<bool>(type: "boolean", nullable: false),
                    TemplateApprovalRequired = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_CommunicationChannelProviderSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommunicationConsentRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecipientType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    RecipientId = table.Column<Guid>(type: "uuid", nullable: true),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Channel = table.Column<int>(type: "integer", nullable: false),
                    Destination = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ConsentCapturedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_CommunicationConsentRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommunicationDeliveryAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientSnapshotId = table.Column<Guid>(type: "uuid", nullable: true),
                    Channel = table.Column<int>(type: "integer", nullable: false),
                    Destination = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ProviderName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ProviderMessageId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    ErrorCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    QueuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeliveredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReadAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EstimatedCost = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    CurrencyCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
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
                    table.PrimaryKey("PK_CommunicationDeliveryAttempts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommunicationRecipientSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    RecipientId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    MobileNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    PreferredLanguageCode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ChannelEligibilityJson = table.Column<string>(type: "text", nullable: false),
                    IsParentDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsEligible = table.Column<bool>(type: "boolean", nullable: false),
                    BlockedReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_CommunicationRecipientSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommunicationReplies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeliveryAttemptId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecipientSnapshotId = table.Column<Guid>(type: "uuid", nullable: true),
                    Channel = table.Column<int>(type: "integer", nullable: false),
                    FromAddress = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AssignedToUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignedDepartment = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    InternalNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_CommunicationReplies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommunicationTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicMediumId = table.Column<Guid>(type: "uuid", nullable: true),
                    TemplateName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    TargetRole = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Subject = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    LanguageCode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ChannelsJson = table.Column<string>(type: "text", nullable: false),
                    VariablesJson = table.Column<string>(type: "text", nullable: false),
                    HashtagsJson = table.Column<string>(type: "text", nullable: false),
                    ApprovalStatus = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_CommunicationTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommunicationUsageLedgerEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: true),
                    Channel = table.Column<int>(type: "integer", nullable: false),
                    ProviderName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    UsageDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    DeliveredCount = table.Column<int>(type: "integer", nullable: false),
                    FailedCount = table.Column<int>(type: "integer", nullable: false),
                    EstimatedCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
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
                    table.PrimaryKey("PK_CommunicationUsageLedgerEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationCampaigns_AnnouncementTypeId",
                table: "CommunicationCampaigns",
                column: "AnnouncementTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationCampaigns_TenantId_ScheduledAt",
                table: "CommunicationCampaigns",
                columns: new[] { "TenantId", "ScheduledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationCampaigns_TenantId_SourceModule_CampaignType",
                table: "CommunicationCampaigns",
                columns: new[] { "TenantId", "SourceModule", "CampaignType" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationCampaigns_TenantId_Status",
                table: "CommunicationCampaigns",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CommProvider_Tenant_Channel_Provider",
                table: "CommunicationChannelProviderSettings",
                columns: new[] { "TenantId", "Channel", "ProviderName" });

            migrationBuilder.CreateIndex(
                name: "IX_CommConsent_Tenant_Channel_Destination",
                table: "CommunicationConsentRecords",
                columns: new[] { "TenantId", "Channel", "Destination" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationDeliveryAttempts_TenantId_CampaignId_Channel",
                table: "CommunicationDeliveryAttempts",
                columns: new[] { "TenantId", "CampaignId", "Channel" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationDeliveryAttempts_TenantId_Status",
                table: "CommunicationDeliveryAttempts",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationRecipientSnapshots_TenantId_CampaignId",
                table: "CommunicationRecipientSnapshots",
                columns: new[] { "TenantId", "CampaignId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationRecipientSnapshots_TenantId_RecipientType_Reci~",
                table: "CommunicationRecipientSnapshots",
                columns: new[] { "TenantId", "RecipientType", "RecipientId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationReplies_TenantId_CampaignId",
                table: "CommunicationReplies",
                columns: new[] { "TenantId", "CampaignId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationReplies_TenantId_Status_ReceivedAt",
                table: "CommunicationReplies",
                columns: new[] { "TenantId", "Status", "ReceivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CommTemplates_Tenant_Medium_Name_Language",
                table: "CommunicationTemplates",
                columns: new[] { "TenantId", "AcademicMediumId", "TemplateName", "LanguageCode" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationUsageLedgerEntries_TenantId_CampaignId",
                table: "CommunicationUsageLedgerEntries",
                columns: new[] { "TenantId", "CampaignId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationUsageLedgerEntries_TenantId_UsageDate_Channel",
                table: "CommunicationUsageLedgerEntries",
                columns: new[] { "TenantId", "UsageDate", "Channel" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommunicationCampaigns");

            migrationBuilder.DropTable(
                name: "CommunicationChannelProviderSettings");

            migrationBuilder.DropTable(
                name: "CommunicationConsentRecords");

            migrationBuilder.DropTable(
                name: "CommunicationDeliveryAttempts");

            migrationBuilder.DropTable(
                name: "CommunicationRecipientSnapshots");

            migrationBuilder.DropTable(
                name: "CommunicationReplies");

            migrationBuilder.DropTable(
                name: "CommunicationTemplates");

            migrationBuilder.DropTable(
                name: "CommunicationUsageLedgerEntries");
        }
    }
}
