using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class StabilizeCommunicationV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CommunicationApprovalRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    RuleName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CampaignType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SourceModule = table.Column<int>(type: "integer", nullable: true),
                    Channel = table.Column<int>(type: "integer", nullable: true),
                    MinimumAudienceSize = table.Column<int>(type: "integer", nullable: true),
                    ImportanceLevel = table.Column<int>(type: "integer", nullable: true),
                    RequiresApproval = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_CommunicationApprovalRules", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationUsageLedgerEntries_CampaignId",
                table: "CommunicationUsageLedgerEntries",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationReplies_CampaignId",
                table: "CommunicationReplies",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationReplies_DeliveryAttemptId",
                table: "CommunicationReplies",
                column: "DeliveryAttemptId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationReplies_RecipientSnapshotId",
                table: "CommunicationReplies",
                column: "RecipientSnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationRecipientSnapshots_CampaignId",
                table: "CommunicationRecipientSnapshots",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationDeliveryAttempts_CampaignId",
                table: "CommunicationDeliveryAttempts",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationDeliveryAttempts_RecipientSnapshotId",
                table: "CommunicationDeliveryAttempts",
                column: "RecipientSnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_CommApprovalRules_Tenant_Scope",
                table: "CommunicationApprovalRules",
                columns: new[] { "TenantId", "CampaignType", "SourceModule", "Channel" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationApprovalRules_TenantId_IsEnabled",
                table: "CommunicationApprovalRules",
                columns: new[] { "TenantId", "IsEnabled" });

            migrationBuilder.AddForeignKey(
                name: "FK_CommunicationDeliveryAttempts_CommunicationCampaigns_Campai~",
                table: "CommunicationDeliveryAttempts",
                column: "CampaignId",
                principalTable: "CommunicationCampaigns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CommunicationDeliveryAttempts_CommunicationRecipientSnapsho~",
                table: "CommunicationDeliveryAttempts",
                column: "RecipientSnapshotId",
                principalTable: "CommunicationRecipientSnapshots",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CommunicationRecipientSnapshots_CommunicationCampaigns_Camp~",
                table: "CommunicationRecipientSnapshots",
                column: "CampaignId",
                principalTable: "CommunicationCampaigns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CommunicationReplies_CommunicationCampaigns_CampaignId",
                table: "CommunicationReplies",
                column: "CampaignId",
                principalTable: "CommunicationCampaigns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CommunicationReplies_CommunicationDeliveryAttempts_Delivery~",
                table: "CommunicationReplies",
                column: "DeliveryAttemptId",
                principalTable: "CommunicationDeliveryAttempts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CommunicationReplies_CommunicationRecipientSnapshots_Recipi~",
                table: "CommunicationReplies",
                column: "RecipientSnapshotId",
                principalTable: "CommunicationRecipientSnapshots",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CommunicationUsageLedgerEntries_CommunicationCampaigns_Camp~",
                table: "CommunicationUsageLedgerEntries",
                column: "CampaignId",
                principalTable: "CommunicationCampaigns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CommunicationDeliveryAttempts_CommunicationCampaigns_Campai~",
                table: "CommunicationDeliveryAttempts");

            migrationBuilder.DropForeignKey(
                name: "FK_CommunicationDeliveryAttempts_CommunicationRecipientSnapsho~",
                table: "CommunicationDeliveryAttempts");

            migrationBuilder.DropForeignKey(
                name: "FK_CommunicationRecipientSnapshots_CommunicationCampaigns_Camp~",
                table: "CommunicationRecipientSnapshots");

            migrationBuilder.DropForeignKey(
                name: "FK_CommunicationReplies_CommunicationCampaigns_CampaignId",
                table: "CommunicationReplies");

            migrationBuilder.DropForeignKey(
                name: "FK_CommunicationReplies_CommunicationDeliveryAttempts_Delivery~",
                table: "CommunicationReplies");

            migrationBuilder.DropForeignKey(
                name: "FK_CommunicationReplies_CommunicationRecipientSnapshots_Recipi~",
                table: "CommunicationReplies");

            migrationBuilder.DropForeignKey(
                name: "FK_CommunicationUsageLedgerEntries_CommunicationCampaigns_Camp~",
                table: "CommunicationUsageLedgerEntries");

            migrationBuilder.DropTable(
                name: "CommunicationApprovalRules");

            migrationBuilder.DropIndex(
                name: "IX_CommunicationUsageLedgerEntries_CampaignId",
                table: "CommunicationUsageLedgerEntries");

            migrationBuilder.DropIndex(
                name: "IX_CommunicationReplies_CampaignId",
                table: "CommunicationReplies");

            migrationBuilder.DropIndex(
                name: "IX_CommunicationReplies_DeliveryAttemptId",
                table: "CommunicationReplies");

            migrationBuilder.DropIndex(
                name: "IX_CommunicationReplies_RecipientSnapshotId",
                table: "CommunicationReplies");

            migrationBuilder.DropIndex(
                name: "IX_CommunicationRecipientSnapshots_CampaignId",
                table: "CommunicationRecipientSnapshots");

            migrationBuilder.DropIndex(
                name: "IX_CommunicationDeliveryAttempts_CampaignId",
                table: "CommunicationDeliveryAttempts");

            migrationBuilder.DropIndex(
                name: "IX_CommunicationDeliveryAttempts_RecipientSnapshotId",
                table: "CommunicationDeliveryAttempts");
        }
    }
}
