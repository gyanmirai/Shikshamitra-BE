using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddAcademicNotificationPipeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CommunicationTemplateRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    EventKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    RecipientKind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Channel = table.Column<int>(type: "integer", nullable: false),
                    LanguageCode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Subject = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Body = table.Column<string>(type: "character varying(65536)", maxLength: 65536, nullable: false),
                    SchemaVersion = table.Column<int>(type: "integer", nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    IsRetired = table.Column<bool>(type: "boolean", nullable: false),
                    PublishedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_CommunicationTemplateRevisions", x => x.Id);
                    table.UniqueConstraint("AK_CommunicationTemplateRevisions_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_CommunicationTemplateRevisions_CommunicationTemplates_Templ~",
                        column: x => x.TemplateId,
                        principalTable: "CommunicationTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NotificationPreferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContextKey = table.Column<string>(type: "character varying(192)", maxLength: 192, nullable: false),
                    Category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ConfigurationJson = table.Column<string>(type: "character varying(65536)", maxLength: 65536, nullable: false),
                    LanguageCode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
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
                    table.PrimaryKey("PK_NotificationPreferences", x => x.Id);
                    table.UniqueConstraint("AK_NotificationPreferences_TenantId_Id", x => new { x.TenantId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "NotificationProviderEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderSettingId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderEventKey = table.Column<string>(type: "character varying(192)", maxLength: 192, nullable: false),
                    ProviderMessageId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ProtectedPayload = table.Column<string>(type: "character varying(131072)", maxLength: 131072, nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_NotificationProviderEvents", x => x.Id);
                    table.UniqueConstraint("AK_NotificationProviderEvents_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_NotificationProviderEvents_CommunicationChannelProviderSett~",
                        column: x => x.ProviderSettingId,
                        principalTable: "CommunicationChannelProviderSettings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NotificationRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ScopeKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: true),
                    BoardId = table.Column<Guid>(type: "uuid", nullable: true),
                    MediumId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClassId = table.Column<Guid>(type: "uuid", nullable: true),
                    DivisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    ConfigurationJson = table.Column<string>(type: "character varying(65536)", maxLength: 65536, nullable: false),
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
                    table.PrimaryKey("PK_NotificationRules", x => x.Id);
                    table.UniqueConstraint("AK_NotificationRules_TenantId_Id", x => new { x.TenantId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "NotificationScheduleCursors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SchedulerKey = table.Column<string>(type: "character varying(192)", maxLength: 192, nullable: false),
                    CursorJson = table.Column<string>(type: "character varying(65536)", maxLength: 65536, nullable: false),
                    NextScanAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LeaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaseExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_NotificationScheduleCursors", x => x.Id);
                    table.UniqueConstraint("AK_NotificationScheduleCursors_TenantId_Id", x => new { x.TenantId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "NotificationSendReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Digest = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConsumedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(192)", maxLength: 192, nullable: true),
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
                    table.PrimaryKey("PK_NotificationSendReviews", x => x.Id);
                    table.UniqueConstraint("AK_NotificationSendReviews_TenantId_Id", x => new { x.TenantId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "WebPushSubscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    BrowserInstallationId = table.Column<Guid>(type: "uuid", nullable: false),
                    EndpointHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ProtectedSubscription = table.Column<string>(type: "character varying(16384)", maxLength: 16384, nullable: false),
                    VapidKeyVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LastActivityAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_WebPushSubscriptions", x => x.Id);
                    table.UniqueConstraint("AK_WebPushSubscriptions_TenantId_Id", x => new { x.TenantId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "WhatsAppTemplateBindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateRevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderSettingId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderTemplateName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ProviderTemplateId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    LanguageCode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ParameterMappingsJson = table.Column<string>(type: "character varying(65536)", maxLength: 65536, nullable: false),
                    ProviderStatus = table.Column<int>(type: "integer", nullable: false),
                    LastSyncedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_WhatsAppTemplateBindings", x => x.Id);
                    table.UniqueConstraint("AK_WhatsAppTemplateBindings_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_WhatsAppTemplateBindings_CommunicationChannelProviderSettin~",
                        column: x => x.ProviderSettingId,
                        principalTable: "CommunicationChannelProviderSettings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WhatsAppTemplateBindings_CommunicationTemplateRevisions_Ten~",
                        columns: x => new { x.TenantId, x.TemplateRevisionId },
                        principalTable: "CommunicationTemplateRevisions",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NotificationIntents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    RuleRevision = table.Column<int>(type: "integer", nullable: false),
                    EventKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    SourceType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceRevision = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeduplicationKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AffectedRecipientsJson = table.Column<string>(type: "character varying(65536)", maxLength: 65536, nullable: false),
                    TemplateRevisionIdsJson = table.Column<string>(type: "character varying(65536)", maxLength: 65536, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    LeaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClaimedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LeaseExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_NotificationIntents", x => x.Id);
                    table.UniqueConstraint("AK_NotificationIntents_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_NotificationIntents_NotificationRules_TenantId_RuleId",
                        columns: x => new { x.TenantId, x.RuleId },
                        principalTable: "NotificationRules",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NotificationDispatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    IntentId = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateRevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientReferenceJson = table.Column<string>(type: "character varying(65536)", maxLength: 65536, nullable: false),
                    RecipientContextKey = table.Column<string>(type: "character varying(192)", maxLength: 192, nullable: false),
                    Channel = table.Column<int>(type: "integer", nullable: false),
                    DestinationFingerprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ProtectedMessageJson = table.Column<string>(type: "character varying(131072)", maxLength: 131072, nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DeduplicationKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecipientSnapshotId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProviderSettingId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeliveryAttemptId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LeaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClaimedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LeaseExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ProviderMessageId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ErrorCode = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
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
                    table.PrimaryKey("PK_NotificationDispatches", x => x.Id);
                    table.UniqueConstraint("AK_NotificationDispatches_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_NotificationDispatches_CommunicationTemplateRevisions_Tenan~",
                        columns: x => new { x.TenantId, x.TemplateRevisionId },
                        principalTable: "CommunicationTemplateRevisions",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NotificationDispatches_NotificationIntents_TenantId_IntentId",
                        columns: x => new { x.TenantId, x.IntentId },
                        principalTable: "NotificationIntents",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NotificationInboxItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DispatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContextKey = table.Column<string>(type: "character varying(192)", maxLength: 192, nullable: false),
                    LogicalKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    RelativeLink = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ReadAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AcknowledgedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_NotificationInboxItems", x => x.Id);
                    table.UniqueConstraint("AK_NotificationInboxItems_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_NotificationInboxItems_NotificationDispatches_TenantId_Disp~",
                        columns: x => new { x.TenantId, x.DispatchId },
                        principalTable: "NotificationDispatches",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationTemplateRevisions_TemplateId",
                table: "CommunicationTemplateRevisions",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "UX_NotificationRevision_Tenant_Template_Version",
                table: "CommunicationTemplateRevisions",
                columns: new[] { "TenantId", "TemplateId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDispatches_Status_NextAttemptAt_TenantId",
                table: "NotificationDispatches",
                columns: new[] { "Status", "NextAttemptAt", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDispatches_TenantId_DeduplicationKey",
                table: "NotificationDispatches",
                columns: new[] { "TenantId", "DeduplicationKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDispatches_TenantId_IntentId",
                table: "NotificationDispatches",
                columns: new[] { "TenantId", "IntentId" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDispatches_TenantId_TemplateRevisionId",
                table: "NotificationDispatches",
                columns: new[] { "TenantId", "TemplateRevisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationInboxItems_TenantId_DispatchId",
                table: "NotificationInboxItems",
                columns: new[] { "TenantId", "DispatchId" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationInboxItems_TenantId_UserId_ReadAt_CreationTime",
                table: "NotificationInboxItems",
                columns: new[] { "TenantId", "UserId", "ReadAt", "CreationTime" });

            migrationBuilder.CreateIndex(
                name: "UX_NotificationInbox_User_Context_Logical",
                table: "NotificationInboxItems",
                columns: new[] { "TenantId", "UserId", "LogicalKey", "ContextKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationIntents_Status_DueAt_TenantId",
                table: "NotificationIntents",
                columns: new[] { "Status", "DueAt", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationIntents_TenantId_DeduplicationKey",
                table: "NotificationIntents",
                columns: new[] { "TenantId", "DeduplicationKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationIntents_TenantId_RuleId",
                table: "NotificationIntents",
                columns: new[] { "TenantId", "RuleId" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationIntents_TenantId_SourceType_SourceId",
                table: "NotificationIntents",
                columns: new[] { "TenantId", "SourceType", "SourceId" });

            migrationBuilder.CreateIndex(
                name: "UX_NotificationPreference_User_Context_Category",
                table: "NotificationPreferences",
                columns: new[] { "TenantId", "UserId", "ContextKey", "Category" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationProviderEvents_ProcessedAt_ReceivedAt",
                table: "NotificationProviderEvents",
                columns: new[] { "ProcessedAt", "ReceivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationProviderEvents_ProviderSettingId",
                table: "NotificationProviderEvents",
                column: "ProviderSettingId");

            migrationBuilder.CreateIndex(
                name: "UX_NotificationProviderEvent_Dedup",
                table: "NotificationProviderEvents",
                columns: new[] { "TenantId", "ProviderSettingId", "ProviderEventKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_NotificationRules_Tenant_Event_Scope",
                table: "NotificationRules",
                columns: new[] { "TenantId", "EventKey", "ScopeKey" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationScheduleCursors_NextScanAt_TenantId",
                table: "NotificationScheduleCursors",
                columns: new[] { "NextScanAt", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationScheduleCursors_TenantId_SchedulerKey",
                table: "NotificationScheduleCursors",
                columns: new[] { "TenantId", "SchedulerKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationSendReviews_ExpiresAt",
                table: "NotificationSendReviews",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "UX_NotificationReviews_Tenant_Actor_Request",
                table: "NotificationSendReviews",
                columns: new[] { "TenantId", "ActorUserId", "IdempotencyKey" },
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WebPushSubscriptions_TenantId_EndpointHash",
                table: "WebPushSubscriptions",
                columns: new[] { "TenantId", "EndpointHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WebPushSubscriptions_TenantId_UserId_IsEnabled",
                table: "WebPushSubscriptions",
                columns: new[] { "TenantId", "UserId", "IsEnabled" });

            migrationBuilder.CreateIndex(
                name: "UX_WebPush_Active_Installation",
                table: "WebPushSubscriptions",
                columns: new[] { "TenantId", "BrowserInstallationId" },
                unique: true,
                filter: "\"IsEnabled\" = true AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppTemplateBindings_ProviderSettingId",
                table: "WhatsAppTemplateBindings",
                column: "ProviderSettingId");

            migrationBuilder.CreateIndex(
                name: "UX_WhatsAppBinding_Tenant_Revision_Provider",
                table: "WhatsAppTemplateBindings",
                columns: new[] { "TenantId", "TemplateRevisionId", "ProviderSettingId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationInboxItems");

            migrationBuilder.DropTable(
                name: "NotificationPreferences");

            migrationBuilder.DropTable(
                name: "NotificationProviderEvents");

            migrationBuilder.DropTable(
                name: "NotificationScheduleCursors");

            migrationBuilder.DropTable(
                name: "NotificationSendReviews");

            migrationBuilder.DropTable(
                name: "WebPushSubscriptions");

            migrationBuilder.DropTable(
                name: "WhatsAppTemplateBindings");

            migrationBuilder.DropTable(
                name: "NotificationDispatches");

            migrationBuilder.DropTable(
                name: "CommunicationTemplateRevisions");

            migrationBuilder.DropTable(
                name: "NotificationIntents");

            migrationBuilder.DropTable(
                name: "NotificationRules");
        }
    }
}
