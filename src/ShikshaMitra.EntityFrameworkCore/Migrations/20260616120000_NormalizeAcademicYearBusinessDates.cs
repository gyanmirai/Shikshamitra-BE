using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShikshaMitra.EntityFrameworkCore;

#nullable disable

namespace ShikshaMitra.Migrations;

[DbContext(typeof(ShikshaMitraDbContext))]
[Migration("20260616120000_NormalizeAcademicYearBusinessDates")]
public partial class NormalizeAcademicYearBusinessDates : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql("""
			UPDATE "AppAcademicYears"
			SET "StartDate" = TIMESTAMPTZ '2026-06-01 00:00:00+00',
				"EndDate" = TIMESTAMPTZ '2027-05-31 00:00:00+00'
			WHERE "Name" = '2026-2027'
				AND "StartDate" = TIMESTAMPTZ '2026-05-31 18:30:00+00'
				AND "EndDate" = TIMESTAMPTZ '2027-05-30 18:30:00+00';
			""");
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
	}
}
