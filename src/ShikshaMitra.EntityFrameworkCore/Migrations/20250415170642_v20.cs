using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v20 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppStudentGroupMembers_AppStudentGroups_StudentGroupId1",
                table: "AppStudentGroupMembers");

            migrationBuilder.DropForeignKey(
                name: "FK_AppStudentGroupMembers_AppStudents_StudentId1",
                table: "AppStudentGroupMembers");

            migrationBuilder.DropIndex(
                name: "IX_AppStudentGroupMembers_StudentGroupId1",
                table: "AppStudentGroupMembers");

            migrationBuilder.DropIndex(
                name: "IX_AppStudentGroupMembers_StudentId1",
                table: "AppStudentGroupMembers");

            migrationBuilder.DropColumn(
                name: "StudentGroupId1",
                table: "AppStudentGroupMembers");

            migrationBuilder.DropColumn(
                name: "StudentId1",
                table: "AppStudentGroupMembers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "StudentGroupId1",
                table: "AppStudentGroupMembers",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "StudentId1",
                table: "AppStudentGroupMembers",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentGroupMembers_StudentGroupId1",
                table: "AppStudentGroupMembers",
                column: "StudentGroupId1");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentGroupMembers_StudentId1",
                table: "AppStudentGroupMembers",
                column: "StudentId1");

            migrationBuilder.AddForeignKey(
                name: "FK_AppStudentGroupMembers_AppStudentGroups_StudentGroupId1",
                table: "AppStudentGroupMembers",
                column: "StudentGroupId1",
                principalTable: "AppStudentGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AppStudentGroupMembers_AppStudents_StudentId1",
                table: "AppStudentGroupMembers",
                column: "StudentId1",
                principalTable: "AppStudents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
