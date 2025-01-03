using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateOnly>(
                name: "DateOfInquiry",
                table: "AppStudentInquiries",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "DateOfBirth",
                table: "AppStudentInquiries",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<string>(
                name: "Age",
                table: "AppStudentInquiries",
                type: "text",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<Guid>(
                name: "EducationBoardId",
                table: "AppStudentInquiries",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentInquiries_EducationBoardId",
                table: "AppStudentInquiries",
                column: "EducationBoardId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppStudentInquiries_AppEducationBoards_EducationBoardId",
                table: "AppStudentInquiries",
                column: "EducationBoardId",
                principalTable: "AppEducationBoards",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppStudentInquiries_AppEducationBoards_EducationBoardId",
                table: "AppStudentInquiries");

            migrationBuilder.DropIndex(
                name: "IX_AppStudentInquiries_EducationBoardId",
                table: "AppStudentInquiries");

            migrationBuilder.DropColumn(
                name: "EducationBoardId",
                table: "AppStudentInquiries");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "DateOfInquiry",
                table: "AppStudentInquiries",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "DateOfBirth",
                table: "AppStudentInquiries",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AlterColumn<int>(
                name: "Age",
                table: "AppStudentInquiries",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");
        }
    }
}
