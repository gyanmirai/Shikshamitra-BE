using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class V18 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppBookCopies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BookId = table.Column<Guid>(type: "uuid", nullable: false),
                    CopyNumber = table.Column<int>(type: "integer", nullable: false),
                    AccessionNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShelfId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppBookCopies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppBookCopies_AppBooks_BookId",
                        column: x => x.BookId,
                        principalTable: "AppBooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppBookCopies_AccessionNumber",
                table: "AppBookCopies",
                column: "AccessionNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppBookCopies_BookId",
                table: "AppBookCopies",
                column: "BookId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppBookCopies");
        }
    }
}
