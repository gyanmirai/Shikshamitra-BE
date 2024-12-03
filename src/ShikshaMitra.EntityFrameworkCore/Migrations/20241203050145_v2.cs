using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IdentificationMark3",
                table: "AppEmployees",
                newName: "SpouseDetails_State");

            migrationBuilder.RenameColumn(
                name: "IdentificationMark2",
                table: "AppEmployees",
                newName: "SpouseDetails_PinCode");

            migrationBuilder.AddColumn<string>(
                name: "FatherDetails_City",
                table: "AppEmployees",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FatherDetails_PinCode",
                table: "AppEmployees",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FatherDetails_State",
                table: "AppEmployees",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotherDetails_City",
                table: "AppEmployees",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotherDetails_PinCode",
                table: "AppEmployees",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotherDetails_State",
                table: "AppEmployees",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SpouseDetails_City",
                table: "AppEmployees",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FatherDetails_City",
                table: "AppEmployees");

            migrationBuilder.DropColumn(
                name: "FatherDetails_PinCode",
                table: "AppEmployees");

            migrationBuilder.DropColumn(
                name: "FatherDetails_State",
                table: "AppEmployees");

            migrationBuilder.DropColumn(
                name: "MotherDetails_City",
                table: "AppEmployees");

            migrationBuilder.DropColumn(
                name: "MotherDetails_PinCode",
                table: "AppEmployees");

            migrationBuilder.DropColumn(
                name: "MotherDetails_State",
                table: "AppEmployees");

            migrationBuilder.DropColumn(
                name: "SpouseDetails_City",
                table: "AppEmployees");

            migrationBuilder.RenameColumn(
                name: "SpouseDetails_State",
                table: "AppEmployees",
                newName: "IdentificationMark3");

            migrationBuilder.RenameColumn(
                name: "SpouseDetails_PinCode",
                table: "AppEmployees",
                newName: "IdentificationMark2");
        }
    }
}
