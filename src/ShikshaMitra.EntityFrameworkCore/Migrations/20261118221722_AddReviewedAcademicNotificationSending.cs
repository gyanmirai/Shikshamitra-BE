using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewedAcademicNotificationSending : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_NotificationIntent_Origin",
                table: "NotificationIntents");

            migrationBuilder.AddColumn<Guid>(
                name: "AssignedApproverUserId",
                table: "NotificationSendReviews",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProtectedRequest",
                table: "NotificationSendReviews",
                type: "character varying(4000000)",
                maxLength: 4000000,
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "RuleId",
                table: "NotificationIntents",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateIndex(
                name: "UX_NotificationReviews_Tenant_Operation",
                table: "NotificationSendReviews",
                columns: new[] { "TenantId", "OperationId" },
                unique: true,
                filter: "\"OperationId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_NotificationIntent_Origin",
                table: "NotificationIntents",
                sql: "((\"RuleId\" IS NOT NULL AND \"RuleId\" <> '00000000-0000-0000-0000-000000000000' AND \"RuleRevision\" > 0) OR (\"Origin\" = 1 AND \"RuleId\" IS NULL AND \"RuleRevision\" = 0)) AND \"Origin\" IN (0, 1) AND (\"Origin\" = 0 OR (\"CampaignId\" IS NOT NULL AND \"ActorUserId\" IS NOT NULL AND \"CampaignId\" <> '00000000-0000-0000-0000-000000000000' AND \"ActorUserId\" <> '00000000-0000-0000-0000-000000000000'))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_NotificationReviews_Tenant_Operation",
                table: "NotificationSendReviews");

            migrationBuilder.DropCheckConstraint(
                name: "CK_NotificationIntent_Origin",
                table: "NotificationIntents");

            migrationBuilder.DropColumn(
                name: "AssignedApproverUserId",
                table: "NotificationSendReviews");

            migrationBuilder.DropColumn(
                name: "ProtectedRequest",
                table: "NotificationSendReviews");

            migrationBuilder.AlterColumn<Guid>(
                name: "RuleId",
                table: "NotificationIntents",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_NotificationIntent_Origin",
                table: "NotificationIntents",
                sql: "\"Origin\" IN (0, 1) AND (\"Origin\" = 0 OR (\"CampaignId\" IS NOT NULL AND \"ActorUserId\" IS NOT NULL AND \"CampaignId\" <> '00000000-0000-0000-0000-000000000000' AND \"ActorUserId\" <> '00000000-0000-0000-0000-000000000000'))");
        }
    }
}
