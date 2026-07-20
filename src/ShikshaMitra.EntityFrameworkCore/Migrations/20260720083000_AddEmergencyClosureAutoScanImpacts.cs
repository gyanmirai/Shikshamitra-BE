using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShikshaMitra.EntityFrameworkCore;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ShikshaMitraDbContext))]
    [Migration("20260720083000_AddEmergencyClosureAutoScanImpacts")]
    public partial class AddEmergencyClosureAutoScanImpacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE IF EXISTS "AppAcademicCalendarDays"
                ADD COLUMN IF NOT EXISTS "AutoScanImpacts" boolean NOT NULL DEFAULT TRUE;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE IF EXISTS "AppAcademicCalendarDays"
                DROP COLUMN IF EXISTS "AutoScanImpacts";
                """);
        }
    }
}
