using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddCommunicationSourceEntityReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SourceEntityId",
                table: "CommunicationCampaigns",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceEntityType",
                table: "CommunicationCampaigns",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceReference",
                table: "CommunicationCampaigns",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommCampaigns_Tenant_SourceEntity",
                table: "CommunicationCampaigns",
                columns: new[] { "TenantId", "SourceModule", "SourceEntityType", "SourceEntityId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CommCampaigns_Tenant_SourceEntity",
                table: "CommunicationCampaigns");

            migrationBuilder.DropColumn(
                name: "SourceEntityId",
                table: "CommunicationCampaigns");

            migrationBuilder.DropColumn(
                name: "SourceEntityType",
                table: "CommunicationCampaigns");

            migrationBuilder.DropColumn(
                name: "SourceReference",
                table: "CommunicationCampaigns");
        }
    }
}
