using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v32 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Color",
                table: "AppStudentGroups",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Logo",
                table: "AppStudentGroups",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Color",
                table: "AppStudentGroups");

            migrationBuilder.DropColumn(
                name: "Logo",
                table: "AppStudentGroups");
        }
    }
}
