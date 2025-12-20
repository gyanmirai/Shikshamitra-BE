using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v5 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppStudents_AppEducationMediums_EducationMediumId",
                table: "AppStudents");

            migrationBuilder.RenameColumn(
                name: "DateOfBirthday",
                table: "AppStudentAdditionalDetails",
                newName: "DateOfBirth");

            migrationBuilder.AlterColumn<Guid>(
                name: "EducationMediumId",
                table: "AppStudents",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "AcademicBoardId",
                table: "AppStudents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AcademicMediumId",
                table: "AppStudents",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddForeignKey(
                name: "FK_AppStudents_AppEducationMediums_EducationMediumId",
                table: "AppStudents",
                column: "EducationMediumId",
                principalTable: "AppEducationMediums",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppStudents_AppEducationMediums_EducationMediumId",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "AcademicBoardId",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "AcademicMediumId",
                table: "AppStudents");

            migrationBuilder.RenameColumn(
                name: "DateOfBirth",
                table: "AppStudentAdditionalDetails",
                newName: "DateOfBirthday");

            migrationBuilder.AlterColumn<Guid>(
                name: "EducationMediumId",
                table: "AppStudents",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AppStudents_AppEducationMediums_EducationMediumId",
                table: "AppStudents",
                column: "EducationMediumId",
                principalTable: "AppEducationMediums",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
