using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class EmployeeApplicationProfileSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdditionalInfo",
                table: "AppEmployeeApplications",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FamilyInfo",
                table: "AppEmployeeApplications",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PersonalInfo",
                table: "AppEmployeeApplications",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdditionalInfo",
                table: "AppEmployeeApplications");

            migrationBuilder.DropColumn(
                name: "FamilyInfo",
                table: "AppEmployeeApplications");

            migrationBuilder.DropColumn(
                name: "PersonalInfo",
                table: "AppEmployeeApplications");
        }
    }
}
