using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShikshaMitra.EntityFrameworkCore;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ShikshaMitraDbContext))]
    [Migration("20260525021500_DueFee_UniqueIndex_PerTenant")]
    public partial class DueFee_UniqueIndex_PerTenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DueFees_StudentId_FeeHeadId_Month_Year",
                table: "DueFees");

            migrationBuilder.CreateIndex(
                name: "IX_DueFees_TenantId_StudentId_FeeHeadId_Month_Year",
                table: "DueFees",
                columns: new[] { "TenantId", "StudentId", "FeeHeadId", "Month", "Year" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DueFees_TenantId_StudentId_FeeHeadId_Month_Year",
                table: "DueFees");

            migrationBuilder.CreateIndex(
                name: "IX_DueFees_StudentId_FeeHeadId_Month_Year",
                table: "DueFees",
                columns: new[] { "StudentId", "FeeHeadId", "Month", "Year" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }
    }
}
