using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v10 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Address",
                table: "AppEmployees",
                newName: "Address3");

            migrationBuilder.AddColumn<string>(
                name: "Address1",
                table: "AppEmployees",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Address2",
                table: "AppEmployees",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Address1",
                table: "AppEmployees");

            migrationBuilder.DropColumn(
                name: "Address2",
                table: "AppEmployees");

            migrationBuilder.RenameColumn(
                name: "Address3",
                table: "AppEmployees",
                newName: "Address");
        }
    }
}
