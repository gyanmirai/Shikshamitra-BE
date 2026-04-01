using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppEmployeeDocuments_EmployeeId",
                table: "AppEmployeeDocuments");

            migrationBuilder.AddColumn<Guid>(
                name: "DocumentNameId",
                table: "AppEmployeeDocuments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastModificationTime",
                table: "AppEmployeeDocuments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDocuments_EmployeeId_DocumentNameId",
                table: "AppEmployeeDocuments",
                columns: new[] { "EmployeeId", "DocumentNameId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EmployeeDocuments_EmployeeId_DocumentNameId",
                table: "AppEmployeeDocuments");

            migrationBuilder.DropColumn(
                name: "DocumentNameId",
                table: "AppEmployeeDocuments");

            migrationBuilder.DropColumn(
                name: "LastModificationTime",
                table: "AppEmployeeDocuments");

            migrationBuilder.AddColumn<Guid>(
                name: "FamilyDetailId",
                table: "AppStudents",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppStudents_FamilyDetailId",
                table: "AppStudents",
                column: "FamilyDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_AppEmployeeDocuments_EmployeeId",
                table: "AppEmployeeDocuments",
                column: "EmployeeId");
        }
    }
}
