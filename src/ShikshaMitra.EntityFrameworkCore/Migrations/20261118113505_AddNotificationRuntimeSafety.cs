using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationRuntimeSafety : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ErrorCode",
                table: "NotificationIntents",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Origin",
                table: "NotificationIntents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PlanningCompletedAt",
                table: "NotificationIntents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RuleConfigurationJson",
                table: "NotificationIntents",
                type: "character varying(65536)",
                maxLength: 65536,
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AcceptedAt",
                table: "NotificationDispatches",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "IoLeaseId",
                table: "NotificationDispatches",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "IoStartedAt",
                table: "NotificationDispatches",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationIntents_TenantId_Status_LeaseExpiresAt",
                table: "NotificationIntents",
                columns: new[] { "TenantId", "Status", "LeaseExpiresAt" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_NotificationIntent_Origin",
                table: "NotificationIntents",
                sql: "\"Origin\" IN (0, 1) AND (\"Origin\" = 0 OR (\"CampaignId\" IS NOT NULL AND \"ActorUserId\" IS NOT NULL AND \"CampaignId\" <> '00000000-0000-0000-0000-000000000000' AND \"ActorUserId\" <> '00000000-0000-0000-0000-000000000000'))");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDispatches_TenantId_Status_LeaseExpiresAt",
                table: "NotificationDispatches",
                columns: new[] { "TenantId", "Status", "LeaseExpiresAt" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_NotificationDispatch_IoMarkers",
                table: "NotificationDispatches",
                sql: "((\"IoStartedAt\" IS NULL AND \"IoLeaseId\" IS NULL) OR (\"IoStartedAt\" IS NOT NULL AND \"IoLeaseId\" IS NOT NULL)) AND (\"AcceptedAt\" IS NULL OR \"IoStartedAt\" IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NotificationIntents_TenantId_Status_LeaseExpiresAt",
                table: "NotificationIntents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_NotificationIntent_Origin",
                table: "NotificationIntents");

            migrationBuilder.DropIndex(
                name: "IX_NotificationDispatches_TenantId_Status_LeaseExpiresAt",
                table: "NotificationDispatches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_NotificationDispatch_IoMarkers",
                table: "NotificationDispatches");

            migrationBuilder.DropColumn(
                name: "ErrorCode",
                table: "NotificationIntents");

            migrationBuilder.DropColumn(
                name: "Origin",
                table: "NotificationIntents");

            migrationBuilder.DropColumn(
                name: "PlanningCompletedAt",
                table: "NotificationIntents");

            migrationBuilder.DropColumn(
                name: "RuleConfigurationJson",
                table: "NotificationIntents");

            migrationBuilder.DropColumn(
                name: "AcceptedAt",
                table: "NotificationDispatches");

            migrationBuilder.DropColumn(
                name: "IoLeaseId",
                table: "NotificationDispatches");

            migrationBuilder.DropColumn(
                name: "IoStartedAt",
                table: "NotificationDispatches");
        }
    }
}
