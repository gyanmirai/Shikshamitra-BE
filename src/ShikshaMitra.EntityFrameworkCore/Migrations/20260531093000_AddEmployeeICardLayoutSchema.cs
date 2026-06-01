using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeICardLayoutSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SchemaVersion",
                table: "AppEmployeeICardTemplates",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "LayoutJson",
                table: "AppEmployeeICardTemplates",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PrintSettingsJson",
                table: "AppEmployeeICardTemplates",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SchemaVersion",
                table: "AppEmployeeICardTemplates");

            migrationBuilder.DropColumn(
                name: "LayoutJson",
                table: "AppEmployeeICardTemplates");

            migrationBuilder.DropColumn(
                name: "PrintSettingsJson",
                table: "AppEmployeeICardTemplates");
        }
    }
}
