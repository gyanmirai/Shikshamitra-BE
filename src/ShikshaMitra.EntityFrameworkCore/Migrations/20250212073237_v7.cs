using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v7 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PassingMark",
                table: "AppEntranceExamSlots");

            migrationBuilder.AddColumn<float>(
                name: "PassingMark",
                table: "AppEntranceExams",
                type: "real",
                nullable: false,
                defaultValue: 0f);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PassingMark",
                table: "AppEntranceExams");

            migrationBuilder.AddColumn<float>(
                name: "PassingMark",
                table: "AppEntranceExamSlots",
                type: "real",
                nullable: false,
                defaultValue: 0f);
        }
    }
}
