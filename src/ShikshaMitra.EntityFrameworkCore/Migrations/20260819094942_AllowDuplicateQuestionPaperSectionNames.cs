using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AllowDuplicateQuestionPaperSectionNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppQuestionPaperSections_QuestionPaperId_SectionName",
                table: "AppQuestionPaperSections");

            migrationBuilder.CreateIndex(
                name: "IX_AppQuestionPaperSections_QuestionPaperId",
                table: "AppQuestionPaperSections",
                column: "QuestionPaperId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppQuestionPaperSections_QuestionPaperId",
                table: "AppQuestionPaperSections");

            migrationBuilder.CreateIndex(
                name: "IX_AppQuestionPaperSections_QuestionPaperId_SectionName",
                table: "AppQuestionPaperSections",
                columns: new[] { "QuestionPaperId", "SectionName" },
                unique: true);
        }
    }
}
