using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v39 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "FormNo",
                table: "AppStudentAdditionalDetails",
                newName: "PreviousSchool");

            migrationBuilder.RenameColumn(
                name: "Category",
                table: "AppStudentAdditionalDetails",
                newName: "PanNo");

            migrationBuilder.AddColumn<string>(
                name: "AdharNo",
                table: "AppStudentAdditionalDetails",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApparId",
                table: "AppStudentAdditionalDetails",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BirthPlace",
                table: "AppStudentAdditionalDetails",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CastCategory",
                table: "AppStudentAdditionalDetails",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FamilyCast",
                table: "AppStudentAdditionalDetails",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LastClassPassed",
                table: "AppStudentAdditionalDetails",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MotherTongue",
                table: "AppStudentAdditionalDetails",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Nationality",
                table: "AppStudentAdditionalDetails",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Religion",
                table: "AppStudentAdditionalDetails",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdharNo",
                table: "AppStudentAdditionalDetails");

            migrationBuilder.DropColumn(
                name: "ApparId",
                table: "AppStudentAdditionalDetails");

            migrationBuilder.DropColumn(
                name: "BirthPlace",
                table: "AppStudentAdditionalDetails");

            migrationBuilder.DropColumn(
                name: "CastCategory",
                table: "AppStudentAdditionalDetails");

            migrationBuilder.DropColumn(
                name: "FamilyCast",
                table: "AppStudentAdditionalDetails");

            migrationBuilder.DropColumn(
                name: "LastClassPassed",
                table: "AppStudentAdditionalDetails");

            migrationBuilder.DropColumn(
                name: "MotherTongue",
                table: "AppStudentAdditionalDetails");

            migrationBuilder.DropColumn(
                name: "Nationality",
                table: "AppStudentAdditionalDetails");

            migrationBuilder.DropColumn(
                name: "Religion",
                table: "AppStudentAdditionalDetails");

            migrationBuilder.RenameColumn(
                name: "PreviousSchool",
                table: "AppStudentAdditionalDetails",
                newName: "FormNo");

            migrationBuilder.RenameColumn(
                name: "PanNo",
                table: "AppStudentAdditionalDetails",
                newName: "Category");
        }
    }
}
