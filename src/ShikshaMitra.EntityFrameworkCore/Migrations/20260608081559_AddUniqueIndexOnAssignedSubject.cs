using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueIndexOnAssignedSubject : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_AppStandardSubjectAssignments_TenantId_StandardId_SubjectId",
                table: "AppStandardSubjectAssignments",
                columns: new[] { "TenantId", "StandardId", "SubjectId" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppStandardSubjectAssignments_TenantId_StandardId_SubjectId",
                table: "AppStandardSubjectAssignments");
        }
    }
}
