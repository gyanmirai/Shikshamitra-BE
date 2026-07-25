using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddAdmissionInquiryRouteDefinition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "IntendedRouteDefinitionId",
                table: "AppAdmissionInquiryLeads",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IntendedRouteDefinitionVersion",
                table: "AppAdmissionInquiryLeads",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntendedRouteNameSnapshot",
                table: "AppAdmissionInquiryLeads",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionInquiryLeads_IntendedRouteDefinitionId",
                table: "AppAdmissionInquiryLeads",
                column: "IntendedRouteDefinitionId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppAdmissionInquiryLeads_AppAdmissionCrmRouteDefinitions_In~",
                table: "AppAdmissionInquiryLeads",
                column: "IntendedRouteDefinitionId",
                principalTable: "AppAdmissionCrmRouteDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppAdmissionInquiryLeads_AppAdmissionCrmRouteDefinitions_In~",
                table: "AppAdmissionInquiryLeads");

            migrationBuilder.DropIndex(
                name: "IX_AppAdmissionInquiryLeads_IntendedRouteDefinitionId",
                table: "AppAdmissionInquiryLeads");

            migrationBuilder.DropColumn(
                name: "IntendedRouteDefinitionId",
                table: "AppAdmissionInquiryLeads");

            migrationBuilder.DropColumn(
                name: "IntendedRouteDefinitionVersion",
                table: "AppAdmissionInquiryLeads");

            migrationBuilder.DropColumn(
                name: "IntendedRouteNameSnapshot",
                table: "AppAdmissionInquiryLeads");
        }
    }
}
