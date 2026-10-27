using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShikshaMitra.EntityFrameworkCore;

namespace ShikshaMitra.Migrations;

[DbContext(typeof(ShikshaMitraDbContext))]
[Migration("20261026193000_AddLeavePolicyVersion")]
public class AddLeavePolicyVersion : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.AddColumn<int>(
		name: "Version", table: "AppEmployeeLeavePolicies", type: "integer", nullable: false, defaultValue: 1);

	protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropColumn(
		name: "Version", table: "AppEmployeeLeavePolicies");
}
