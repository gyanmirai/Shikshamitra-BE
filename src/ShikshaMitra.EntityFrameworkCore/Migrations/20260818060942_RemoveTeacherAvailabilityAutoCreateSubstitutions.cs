using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class RemoveTeacherAvailabilityAutoCreateSubstitutions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoCreateSubstitutions",
                table: "AppAcademicTeacherAvailabilities");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutoCreateSubstitutions",
                table: "AppAcademicTeacherAvailabilities",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
