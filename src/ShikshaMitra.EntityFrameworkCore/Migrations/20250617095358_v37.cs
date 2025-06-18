using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v37 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Books_AppBookCategories_BookCategoryId",
                table: "Books");

            migrationBuilder.DropForeignKey(
                name: "FK_BooksAssignments_Books_BookId",
                table: "BooksAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_BooksAssignments_Members_MemberId",
                table: "BooksAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_BooksReservations_Books_BookId",
                table: "BooksReservations");

            migrationBuilder.DropForeignKey(
                name: "FK_BooksReservations_Members_MemberId",
                table: "BooksReservations");

            migrationBuilder.DropForeignKey(
                name: "FK_Fines_BooksAssignments_BookAssignmentId",
                table: "Fines");

            migrationBuilder.DropForeignKey(
                name: "FK_Fines_Members_MemberId",
                table: "Fines");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Members",
                table: "Members");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Fines",
                table: "Fines");

            migrationBuilder.DropPrimaryKey(
                name: "PK_BooksReservations",
                table: "BooksReservations");

            migrationBuilder.DropPrimaryKey(
                name: "PK_BooksAssignments",
                table: "BooksAssignments");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Books",
                table: "Books");

            migrationBuilder.RenameTable(
                name: "Members",
                newName: "AppMembers");

            migrationBuilder.RenameTable(
                name: "Fines",
                newName: "AppFines");

            migrationBuilder.RenameTable(
                name: "BooksReservations",
                newName: "AppBooksReservations");

            migrationBuilder.RenameTable(
                name: "BooksAssignments",
                newName: "AppBooksAssignments");

            migrationBuilder.RenameTable(
                name: "Books",
                newName: "AppBooks");

            migrationBuilder.RenameIndex(
                name: "IX_Fines_MemberId",
                table: "AppFines",
                newName: "IX_AppFines_MemberId");

            migrationBuilder.RenameIndex(
                name: "IX_Fines_BookAssignmentId",
                table: "AppFines",
                newName: "IX_AppFines_BookAssignmentId");

            migrationBuilder.RenameIndex(
                name: "IX_BooksReservations_MemberId",
                table: "AppBooksReservations",
                newName: "IX_AppBooksReservations_MemberId");

            migrationBuilder.RenameIndex(
                name: "IX_BooksReservations_BookId",
                table: "AppBooksReservations",
                newName: "IX_AppBooksReservations_BookId");

            migrationBuilder.RenameIndex(
                name: "IX_BooksAssignments_MemberId",
                table: "AppBooksAssignments",
                newName: "IX_AppBooksAssignments_MemberId");

            migrationBuilder.RenameIndex(
                name: "IX_BooksAssignments_BookId",
                table: "AppBooksAssignments",
                newName: "IX_AppBooksAssignments_BookId");

            migrationBuilder.RenameIndex(
                name: "IX_Books_BookCategoryId",
                table: "AppBooks",
                newName: "IX_AppBooks_BookCategoryId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AppMembers",
                table: "AppMembers",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AppFines",
                table: "AppFines",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AppBooksReservations",
                table: "AppBooksReservations",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AppBooksAssignments",
                table: "AppBooksAssignments",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AppBooks",
                table: "AppBooks",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AppBooks_AppBookCategories_BookCategoryId",
                table: "AppBooks",
                column: "BookCategoryId",
                principalTable: "AppBookCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AppBooksAssignments_AppBooks_BookId",
                table: "AppBooksAssignments",
                column: "BookId",
                principalTable: "AppBooks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AppBooksAssignments_AppMembers_MemberId",
                table: "AppBooksAssignments",
                column: "MemberId",
                principalTable: "AppMembers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

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
                name: "FK_AppFines_AppBooksAssignments_BookAssignmentId",
                table: "AppFines",
                column: "BookAssignmentId",
                principalTable: "AppBooksAssignments",
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppBooks_AppBookCategories_BookCategoryId",
                table: "AppBooks");

            migrationBuilder.DropForeignKey(
                name: "FK_AppBooksAssignments_AppBooks_BookId",
                table: "AppBooksAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_AppBooksAssignments_AppMembers_MemberId",
                table: "AppBooksAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_AppBooksReservations_AppBooks_BookId",
                table: "AppBooksReservations");

            migrationBuilder.DropForeignKey(
                name: "FK_AppBooksReservations_AppMembers_MemberId",
                table: "AppBooksReservations");

            migrationBuilder.DropForeignKey(
                name: "FK_AppFines_AppBooksAssignments_BookAssignmentId",
                table: "AppFines");

            migrationBuilder.DropForeignKey(
                name: "FK_AppFines_AppMembers_MemberId",
                table: "AppFines");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AppMembers",
                table: "AppMembers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AppFines",
                table: "AppFines");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AppBooksReservations",
                table: "AppBooksReservations");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AppBooksAssignments",
                table: "AppBooksAssignments");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AppBooks",
                table: "AppBooks");

            migrationBuilder.RenameTable(
                name: "AppMembers",
                newName: "Members");

            migrationBuilder.RenameTable(
                name: "AppFines",
                newName: "Fines");

            migrationBuilder.RenameTable(
                name: "AppBooksReservations",
                newName: "BooksReservations");

            migrationBuilder.RenameTable(
                name: "AppBooksAssignments",
                newName: "BooksAssignments");

            migrationBuilder.RenameTable(
                name: "AppBooks",
                newName: "Books");

            migrationBuilder.RenameIndex(
                name: "IX_AppFines_MemberId",
                table: "Fines",
                newName: "IX_Fines_MemberId");

            migrationBuilder.RenameIndex(
                name: "IX_AppFines_BookAssignmentId",
                table: "Fines",
                newName: "IX_Fines_BookAssignmentId");

            migrationBuilder.RenameIndex(
                name: "IX_AppBooksReservations_MemberId",
                table: "BooksReservations",
                newName: "IX_BooksReservations_MemberId");

            migrationBuilder.RenameIndex(
                name: "IX_AppBooksReservations_BookId",
                table: "BooksReservations",
                newName: "IX_BooksReservations_BookId");

            migrationBuilder.RenameIndex(
                name: "IX_AppBooksAssignments_MemberId",
                table: "BooksAssignments",
                newName: "IX_BooksAssignments_MemberId");

            migrationBuilder.RenameIndex(
                name: "IX_AppBooksAssignments_BookId",
                table: "BooksAssignments",
                newName: "IX_BooksAssignments_BookId");

            migrationBuilder.RenameIndex(
                name: "IX_AppBooks_BookCategoryId",
                table: "Books",
                newName: "IX_Books_BookCategoryId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Members",
                table: "Members",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Fines",
                table: "Fines",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_BooksReservations",
                table: "BooksReservations",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_BooksAssignments",
                table: "BooksAssignments",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Books",
                table: "Books",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Books_AppBookCategories_BookCategoryId",
                table: "Books",
                column: "BookCategoryId",
                principalTable: "AppBookCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BooksAssignments_Books_BookId",
                table: "BooksAssignments",
                column: "BookId",
                principalTable: "Books",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BooksAssignments_Members_MemberId",
                table: "BooksAssignments",
                column: "MemberId",
                principalTable: "Members",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BooksReservations_Books_BookId",
                table: "BooksReservations",
                column: "BookId",
                principalTable: "Books",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BooksReservations_Members_MemberId",
                table: "BooksReservations",
                column: "MemberId",
                principalTable: "Members",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Fines_BooksAssignments_BookAssignmentId",
                table: "Fines",
                column: "BookAssignmentId",
                principalTable: "BooksAssignments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Fines_Members_MemberId",
                table: "Fines",
                column: "MemberId",
                principalTable: "Members",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
