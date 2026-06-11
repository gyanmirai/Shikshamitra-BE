using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceImportOriginalFileMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OriginalFileBlobName",
                table: "HR_AttendanceImportBatches",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginalFileContentType",
                table: "HR_AttendanceImportBatches",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OriginalFileBlobName",
                table: "HR_AttendanceImportBatches");

            migrationBuilder.DropColumn(
                name: "OriginalFileContentType",
                table: "HR_AttendanceImportBatches");
        }
    }
}
