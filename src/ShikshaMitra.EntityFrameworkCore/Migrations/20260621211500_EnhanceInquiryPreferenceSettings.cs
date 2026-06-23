using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class EnhanceInquiryPreferenceSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeactivationReason",
                table: "AppInquiryPreferences",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "AppInquiryPreferences",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DisplayOrder",
                table: "AppInquiryPreferences",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "AppInquiryPreferences",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeactivationReason",
                table: "AppInquiryPreferences");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "AppInquiryPreferences");

            migrationBuilder.DropColumn(
                name: "DisplayOrder",
                table: "AppInquiryPreferences");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "AppInquiryPreferences");
        }
    }
}
