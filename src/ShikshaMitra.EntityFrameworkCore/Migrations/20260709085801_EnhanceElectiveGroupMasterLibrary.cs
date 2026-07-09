using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class EnhanceElectiveGroupMasterLibrary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ArchiveReason",
                table: "AppElectiveGroups",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "AppElectiveGroups",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GroupType",
                table: "AppElectiveGroups",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "AppElectiveGroups",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "AppElectiveGroups",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArchiveReason",
                table: "AppElectiveGroups");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "AppElectiveGroups");

            migrationBuilder.DropColumn(
                name: "GroupType",
                table: "AppElectiveGroups");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "AppElectiveGroups");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "AppElectiveGroups");
        }
    }
}
