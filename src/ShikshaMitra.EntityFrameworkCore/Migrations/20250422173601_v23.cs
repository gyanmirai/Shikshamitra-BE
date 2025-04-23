using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v23 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDivisionLeader",
                table: "AppStudentGroupMembers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentGroupMembers_StudentGroupId_IsDivisionLeader",
                table: "AppStudentGroupMembers",
                columns: new[] { "StudentGroupId", "IsDivisionLeader" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppStudentGroupMembers_StudentGroupId_IsDivisionLeader",
                table: "AppStudentGroupMembers");

            migrationBuilder.DropColumn(
                name: "IsDivisionLeader",
                table: "AppStudentGroupMembers");
        }
    }
}
