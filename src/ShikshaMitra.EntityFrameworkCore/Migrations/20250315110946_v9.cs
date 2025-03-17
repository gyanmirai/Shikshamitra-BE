using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v9 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SpouseDetails_Address",
                table: "AppEmployees",
                newName: "SpouseDetails_Address3");

            migrationBuilder.RenameColumn(
                name: "MotherDetails_Address",
                table: "AppEmployees",
                newName: "SpouseDetails_Address2");

            migrationBuilder.RenameColumn(
                name: "FatherDetails_Address",
                table: "AppEmployees",
                newName: "SpouseDetails_Address1");

            migrationBuilder.AddColumn<string>(
                name: "FatherDetails_Address1",
                table: "AppEmployees",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FatherDetails_Address2",
                table: "AppEmployees",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FatherDetails_Address3",
                table: "AppEmployees",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotherDetails_Address1",
                table: "AppEmployees",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotherDetails_Address2",
                table: "AppEmployees",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotherDetails_Address3",
                table: "AppEmployees",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FatherDetails_Address1",
                table: "AppEmployees");

            migrationBuilder.DropColumn(
                name: "FatherDetails_Address2",
                table: "AppEmployees");

            migrationBuilder.DropColumn(
                name: "FatherDetails_Address3",
                table: "AppEmployees");

            migrationBuilder.DropColumn(
                name: "MotherDetails_Address1",
                table: "AppEmployees");

            migrationBuilder.DropColumn(
                name: "MotherDetails_Address2",
                table: "AppEmployees");

            migrationBuilder.DropColumn(
                name: "MotherDetails_Address3",
                table: "AppEmployees");

            migrationBuilder.RenameColumn(
                name: "SpouseDetails_Address3",
                table: "AppEmployees",
                newName: "SpouseDetails_Address");

            migrationBuilder.RenameColumn(
                name: "SpouseDetails_Address2",
                table: "AppEmployees",
                newName: "MotherDetails_Address");

            migrationBuilder.RenameColumn(
                name: "SpouseDetails_Address1",
                table: "AppEmployees",
                newName: "FatherDetails_Address");
        }
    }
}
