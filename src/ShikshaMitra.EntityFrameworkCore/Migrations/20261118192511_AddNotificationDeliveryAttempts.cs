using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationDeliveryAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "NotificationDispatchId",
                table: "CommunicationDeliveryAttempts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "NotificationProviderSettingId",
                table: "CommunicationDeliveryAttempts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryAttempt_NotificationQuota",
                table: "CommunicationDeliveryAttempts",
                columns: new[] { "TenantId", "NotificationProviderSettingId", "QueuedAt" },
                filter: "\"NotificationProviderSettingId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_DeliveryAttempt_NotificationAttempt",
                table: "CommunicationDeliveryAttempts",
                columns: new[] { "TenantId", "NotificationDispatchId", "AttemptNumber" },
                unique: true,
                filter: "\"NotificationDispatchId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_CommunicationDeliveryAttempts_NotificationDispatches_Tenant~",
                table: "CommunicationDeliveryAttempts",
                columns: new[] { "TenantId", "NotificationDispatchId" },
                principalTable: "NotificationDispatches",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CommunicationDeliveryAttempts_NotificationDispatches_Tenant~",
                table: "CommunicationDeliveryAttempts");

            migrationBuilder.DropIndex(
                name: "IX_DeliveryAttempt_NotificationQuota",
                table: "CommunicationDeliveryAttempts");

            migrationBuilder.DropIndex(
                name: "UX_DeliveryAttempt_NotificationAttempt",
                table: "CommunicationDeliveryAttempts");

            migrationBuilder.DropColumn(
                name: "NotificationDispatchId",
                table: "CommunicationDeliveryAttempts");

            migrationBuilder.DropColumn(
                name: "NotificationProviderSettingId",
                table: "CommunicationDeliveryAttempts");
        }
    }
}
