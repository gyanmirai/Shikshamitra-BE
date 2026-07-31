using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddQuestionBankTopicName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TopicName",
                table: "AppQuestionBankItems",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppQuestionBankItems_TopicName",
                table: "AppQuestionBankItems",
                column: "TopicName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppQuestionBankItems_TopicName",
                table: "AppQuestionBankItems");

            migrationBuilder.DropColumn(
                name: "TopicName",
                table: "AppQuestionBankItems");
        }
    }
}
