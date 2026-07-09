using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class EnhanceSubjectMasterLibrary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ArchiveReason",
                table: "AppSubjects",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "AppSubjects",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "AppSubjects",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "AppSubjects",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Icon",
                table: "AppSubjects",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "AppSubjects",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "AppSubjects",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.DropColumn(
                name: "ArchiveReason",
                table: "AppSubjects");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "AppSubjects");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "AppSubjects");

            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "AppSubjects");

            migrationBuilder.DropColumn(
                name: "Icon",
                table: "AppSubjects");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "AppSubjects");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "AppSubjects");
        }
    }
}
