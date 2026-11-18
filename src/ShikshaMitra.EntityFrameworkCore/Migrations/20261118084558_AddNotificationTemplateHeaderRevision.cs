using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationTemplateHeaderRevision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CurrentNotificationRevisionId",
                table: "CommunicationTemplates",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_CommunicationTemplateRevisions_TenantId_TemplateId_Id",
                table: "CommunicationTemplateRevisions",
                columns: new[] { "TenantId", "TemplateId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_CommTemplate_CurrentRevision",
                table: "CommunicationTemplates",
                columns: new[] { "TenantId", "Id", "CurrentNotificationRevisionId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_CommTemplate_NotificationTenant",
                table: "CommunicationTemplates",
                sql: "\"CurrentNotificationRevisionId\" IS NULL OR \"TenantId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_CommTemplate_CurrentRevision",
                table: "CommunicationTemplates",
                columns: new[] { "TenantId", "Id", "CurrentNotificationRevisionId" },
                principalTable: "CommunicationTemplateRevisions",
                principalColumns: new[] { "TenantId", "TemplateId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CommTemplate_CurrentRevision",
                table: "CommunicationTemplates");

            migrationBuilder.DropIndex(
                name: "IX_CommTemplate_CurrentRevision",
                table: "CommunicationTemplates");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CommTemplate_NotificationTenant",
                table: "CommunicationTemplates");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_CommunicationTemplateRevisions_TenantId_TemplateId_Id",
                table: "CommunicationTemplateRevisions");

            migrationBuilder.DropColumn(
                name: "CurrentNotificationRevisionId",
                table: "CommunicationTemplates");
        }
    }
}
