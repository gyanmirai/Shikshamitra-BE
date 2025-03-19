using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v11 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DivisionId",
                table: "AppStudentEnrollments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentEnrollments_DivisionId",
                table: "AppStudentEnrollments",
                column: "DivisionId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppStudentEnrollments_AppDivisions_DivisionId",
                table: "AppStudentEnrollments",
                column: "DivisionId",
                principalTable: "AppDivisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppStudentEnrollments_AppDivisions_DivisionId",
                table: "AppStudentEnrollments");

            migrationBuilder.DropIndex(
                name: "IX_AppStudentEnrollments_DivisionId",
                table: "AppStudentEnrollments");

            migrationBuilder.DropColumn(
                name: "DivisionId",
                table: "AppStudentEnrollments");
        }
    }
}
