using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShikshaMitra.EntityFrameworkCore;

#nullable disable

namespace ShikshaMitra.Migrations;

[DbContext(typeof(ShikshaMitraDbContext))]
[Migration("20260814170000_AddAdmissionCycleIsDeletedDefault")]
public partial class AddAdmissionCycleIsDeletedDefault : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.AlterColumn<bool>(
			name: "IsDeleted",
			table: "AppAdmissionCycles",
			type: "boolean",
			nullable: false,
			defaultValue: false,
			oldClrType: typeof(bool),
			oldType: "boolean");
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.AlterColumn<bool>(
			name: "IsDeleted",
			table: "AppAdmissionCycles",
			type: "boolean",
			nullable: false,
			oldClrType: typeof(bool),
			oldType: "boolean",
			oldDefaultValue: false);
	}
}
