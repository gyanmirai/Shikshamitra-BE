using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddAdmissionIntakeProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CounsellorAssignmentProfileId",
                table: "AppAdmissionOfferings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InquiryIntakeProfileId",
                table: "AppAdmissionOfferings",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppAdmissionIntakeProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ConfigurationJson = table.Column<string>(type: "text", nullable: false),
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
                    table.PrimaryKey("PK_AppAdmissionIntakeProfiles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionOfferings_CounsellorAssignmentProfileId",
                table: "AppAdmissionOfferings",
                column: "CounsellorAssignmentProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionOfferings_InquiryIntakeProfileId",
                table: "AppAdmissionOfferings",
                column: "InquiryIntakeProfileId");

            migrationBuilder.CreateIndex(
                name: "UX_AdmissionIntakeProfiles_TenantKindCode",
                table: "AppAdmissionIntakeProfiles",
                columns: new[] { "TenantId", "Kind", "Code" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.AddForeignKey(
                name: "FK_AppAdmissionOfferings_AppAdmissionIntakeProfiles_Counsellor~",
                table: "AppAdmissionOfferings",
                column: "CounsellorAssignmentProfileId",
                principalTable: "AppAdmissionIntakeProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AppAdmissionOfferings_AppAdmissionIntakeProfiles_InquiryInt~",
                table: "AppAdmissionOfferings",
                column: "InquiryIntakeProfileId",
                principalTable: "AppAdmissionIntakeProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppAdmissionOfferings_AppAdmissionIntakeProfiles_Counsellor~",
                table: "AppAdmissionOfferings");

            migrationBuilder.DropForeignKey(
                name: "FK_AppAdmissionOfferings_AppAdmissionIntakeProfiles_InquiryInt~",
                table: "AppAdmissionOfferings");

            migrationBuilder.DropTable(
                name: "AppAdmissionIntakeProfiles");

            migrationBuilder.DropIndex(
                name: "IX_AppAdmissionOfferings_CounsellorAssignmentProfileId",
                table: "AppAdmissionOfferings");

            migrationBuilder.DropIndex(
                name: "IX_AppAdmissionOfferings_InquiryIntakeProfileId",
                table: "AppAdmissionOfferings");

            migrationBuilder.DropColumn(
                name: "CounsellorAssignmentProfileId",
                table: "AppAdmissionOfferings");

            migrationBuilder.DropColumn(
                name: "InquiryIntakeProfileId",
                table: "AppAdmissionOfferings");
        }
    }
}
