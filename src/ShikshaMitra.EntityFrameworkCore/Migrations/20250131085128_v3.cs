using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RoomNo",
                table: "AppEntranceExamSlots",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StudentLimit",
                table: "AppEntranceExamSlots",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<float>(
                name: "MarksObtained",
                table: "AppEntranceExams",
                type: "real",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RoomNo",
                table: "AppEntranceExamSlots");

            migrationBuilder.DropColumn(
                name: "StudentLimit",
                table: "AppEntranceExamSlots");

            migrationBuilder.DropColumn(
                name: "MarksObtained",
                table: "AppEntranceExams");
        }
    }
}
