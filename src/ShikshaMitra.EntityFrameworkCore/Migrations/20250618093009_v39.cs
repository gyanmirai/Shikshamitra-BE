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
            migrationBuilder.DropForeignKey(
                name: "FK_AppFines_AppBooksAssignments_BookAssignmentId",
                table: "AppFines");

            migrationBuilder.RenameColumn(
                name: "BookAssignmentId",
                table: "AppFines",
                newName: "BooksAssignmentId");

            migrationBuilder.RenameIndex(
                name: "IX_AppFines_BookAssignmentId",
                table: "AppFines",
                newName: "IX_AppFines_BooksAssignmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppFines_AppBooksAssignments_BooksAssignmentId",
                table: "AppFines",
                column: "BooksAssignmentId",
                principalTable: "AppBooksAssignments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppFines_AppBooksAssignments_BooksAssignmentId",
                table: "AppFines");

            migrationBuilder.RenameColumn(
                name: "BooksAssignmentId",
                table: "AppFines",
                newName: "BookAssignmentId");

            migrationBuilder.RenameIndex(
                name: "IX_AppFines_BooksAssignmentId",
                table: "AppFines",
                newName: "IX_AppFines_BookAssignmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppFines_AppBooksAssignments_BookAssignmentId",
                table: "AppFines",
                column: "BookAssignmentId",
                principalTable: "AppBooksAssignments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
