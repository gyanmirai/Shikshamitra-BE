using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v34 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                table: "AppEventAndHolidays",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "AppEventAndHolidays",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SecondaryMobileNo",
                table: "AppEmployees",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "DocumentSize",
                table: "AppDocumentSettings",
                type: "real",
                nullable: false,
                defaultValue: 0f);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Name",
                table: "AppEventAndHolidays");

            migrationBuilder.DropColumn(
                name: "SecondaryMobileNo",
                table: "AppEmployees");

            migrationBuilder.DropColumn(
                name: "DocumentSize",
                table: "AppDocumentSettings");

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                table: "AppEventAndHolidays",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
