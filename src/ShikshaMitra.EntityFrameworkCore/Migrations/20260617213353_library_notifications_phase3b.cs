using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class library_notifications_phase3b : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppLibraryNotificationHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    EventGroup = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    EventName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Channel = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    RecipientRole = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    RecipientMemberId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecipientName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RecipientEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Subject = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RelatedEntityType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    RelatedEntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeduplicationKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ReminderDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppLibraryNotificationHistories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppLibraryNotificationSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    EventGroup = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    EventName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    RecipientRole = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Channel = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsChannelAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    IsComingSoon = table.Column<bool>(type: "boolean", nullable: false),
                    ReminderOffsetDays = table.Column<int>(type: "integer", nullable: true),
                    SubjectTemplate = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    MessageTemplate = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppLibraryNotificationSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryNotificationHistories_Channel",
                table: "AppLibraryNotificationHistories",
                column: "Channel");

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryNotificationHistories_DeduplicationKey",
                table: "AppLibraryNotificationHistories",
                column: "DeduplicationKey");

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryNotificationHistories_EventType",
                table: "AppLibraryNotificationHistories",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryNotificationHistories_RecipientMemberId",
                table: "AppLibraryNotificationHistories",
                column: "RecipientMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryNotificationHistories_RelatedEntityId",
                table: "AppLibraryNotificationHistories",
                column: "RelatedEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryNotificationHistories_Status",
                table: "AppLibraryNotificationHistories",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryNotificationSettings_EventGroup",
                table: "AppLibraryNotificationSettings",
                column: "EventGroup");

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryNotificationSettings_EventType_RecipientRole_Chan~",
                table: "AppLibraryNotificationSettings",
                columns: new[] { "EventType", "RecipientRole", "Channel" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppLibraryNotificationHistories");

            migrationBuilder.DropTable(
                name: "AppLibraryNotificationSettings");
        }
    }
}
