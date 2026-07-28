using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddAdmissionCrmRegistrationProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DataEntryState",
                table: "AppAdmissionCrmRegistrationApplications",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PhysicalFormNumber",
                table: "AppAdmissionCrmRegistrationApplications",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PhysicalFormReceivedByUserId",
                table: "AppAdmissionCrmRegistrationApplications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PhysicalFormReceivedDate",
                table: "AppAdmissionCrmRegistrationApplications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Provenance",
                table: "AppAdmissionCrmRegistrationApplications",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ScannedFormAttachmentReference",
                table: "AppAdmissionCrmRegistrationApplications",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DataEntryState",
                table: "AppAdmissionCrmRegistrationApplications");

            migrationBuilder.DropColumn(
                name: "PhysicalFormNumber",
                table: "AppAdmissionCrmRegistrationApplications");

            migrationBuilder.DropColumn(
                name: "PhysicalFormReceivedByUserId",
                table: "AppAdmissionCrmRegistrationApplications");

            migrationBuilder.DropColumn(
                name: "PhysicalFormReceivedDate",
                table: "AppAdmissionCrmRegistrationApplications");

            migrationBuilder.DropColumn(
                name: "Provenance",
                table: "AppAdmissionCrmRegistrationApplications");

            migrationBuilder.DropColumn(
                name: "ScannedFormAttachmentReference",
                table: "AppAdmissionCrmRegistrationApplications");
        }
    }
}
