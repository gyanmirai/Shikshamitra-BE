using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class DueFee_PartialUniqueIndex_ExcludeSoftDeleted : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DueFees_StudentId_FeeHeadId_Month_Year",
                table: "DueFees");

            migrationBuilder.CreateIndex(
                name: "IX_DueFees_StudentId_FeeHeadId_Month_Year",
                table: "DueFees",
                columns: new[] { "StudentId", "FeeHeadId", "Month", "Year" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DueFees_StudentId_FeeHeadId_Month_Year",
                table: "DueFees");

            migrationBuilder.CreateIndex(
                name: "IX_DueFees_StudentId_FeeHeadId_Month_Year",
                table: "DueFees",
                columns: new[] { "StudentId", "FeeHeadId", "Month", "Year" },
                unique: true);
        }
    }
}
