using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShikshaMitra.EntityFrameworkCore;

namespace ShikshaMitra.Migrations;

[DbContext(typeof(ShikshaMitraDbContext))]
[Migration("20261119180000_MoveHouseLeadershipToDivisions")]
public class MoveHouseLeadershipToDivisions : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		// Preserve this migration ID wherever it already ran. Pending databases
		// keep their house appointments and skip the superseded division transfer.
	}

	protected override void Down(MigrationBuilder migrationBuilder) =>
		throw new NotSupportedException("House leadership migration history must be rolled back with a reviewed database restore.");
}
