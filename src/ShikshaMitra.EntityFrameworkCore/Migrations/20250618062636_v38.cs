using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v38 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppBooksReservations_AppBooks_BookId",
                table: "AppBooksReservations");

            migrationBuilder.DropForeignKey(
                name: "FK_AppBooksReservations_AppMembers_MemberId",
                table: "AppBooksReservations");

            migrationBuilder.DropForeignKey(
                name: "FK_AppFines_AppMembers_MemberId",
                table: "AppFines");

            migrationBuilder.DropIndex(
                name: "IX_AppFines_MemberId",
                table: "AppFines");

            migrationBuilder.DropIndex(
                name: "IX_AppBooksReservations_BookId",
                table: "AppBooksReservations");

            migrationBuilder.DropColumn(
                name: "MemberId",
                table: "AppFines");

            migrationBuilder.DropColumn(
                name: "BookId",
                table: "AppBooksReservations");

            migrationBuilder.RenameColumn(
                name: "MemberId",
                table: "AppBooksReservations",
                newName: "BooksAssignmentId");

            migrationBuilder.RenameIndex(
                name: "IX_AppBooksReservations_MemberId",
                table: "AppBooksReservations",
                newName: "IX_AppBooksReservations_BooksAssignmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppBooksReservations_AppBooksAssignments_BooksAssignmentId",
                table: "AppBooksReservations",
                column: "BooksAssignmentId",
                principalTable: "AppBooksAssignments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppBooksReservations_AppBooksAssignments_BooksAssignmentId",
                table: "AppBooksReservations");

            migrationBuilder.RenameColumn(
                name: "BooksAssignmentId",
                table: "AppBooksReservations",
                newName: "MemberId");

            migrationBuilder.RenameIndex(
                name: "IX_AppBooksReservations_BooksAssignmentId",
                table: "AppBooksReservations",
                newName: "IX_AppBooksReservations_MemberId");

            migrationBuilder.AddColumn<Guid>(
                name: "MemberId",
                table: "AppFines",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BookId",
                table: "AppBooksReservations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_AppFines_MemberId",
                table: "AppFines",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_AppBooksReservations_BookId",
                table: "AppBooksReservations",
                column: "BookId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppBooksReservations_AppBooks_BookId",
                table: "AppBooksReservations",
                column: "BookId",
                principalTable: "AppBooks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AppBooksReservations_AppMembers_MemberId",
                table: "AppBooksReservations",
                column: "MemberId",
                principalTable: "AppMembers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AppFines_AppMembers_MemberId",
                table: "AppFines",
                column: "MemberId",
                principalTable: "AppMembers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
