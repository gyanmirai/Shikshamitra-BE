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
            migrationBuilder.DropPrimaryKey(
                name: "PK_StudentCertificates",
                table: "StudentCertificates");

            migrationBuilder.RenameTable(
                name: "StudentCertificates",
                newName: "AppStudentCertificates");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AppStudentCertificates",
                table: "AppStudentCertificates",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentCertificates_StudentId",
                table: "AppStudentCertificates",
                column: "StudentId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppStudentCertificates_AppStudents_StudentId",
                table: "AppStudentCertificates",
                column: "StudentId",
                principalTable: "AppStudents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppStudentCertificates_AppStudents_StudentId",
                table: "AppStudentCertificates");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AppStudentCertificates",
                table: "AppStudentCertificates");

            migrationBuilder.DropIndex(
                name: "IX_AppStudentCertificates_StudentId",
                table: "AppStudentCertificates");

            migrationBuilder.RenameTable(
                name: "AppStudentCertificates",
                newName: "StudentCertificates");

            migrationBuilder.AddPrimaryKey(
                name: "PK_StudentCertificates",
                table: "StudentCertificates",
                column: "Id");
        }
    }
}
