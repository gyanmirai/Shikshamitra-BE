using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations;

public partial class AddShiftTenantIsolation : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.AddColumn<Guid>(
			name: "TenantId",
			table: "academic_ShiftManagements",
			type: "uuid",
			nullable: true);

		// Legacy references may come from the leak; ownership needs a verified, audited repair.
		// Leave existing rows host-only rather than assigning another school's data by inference.

		migrationBuilder.CreateIndex(
			name: "IX_academic_ShiftManagements_TenantId",
			table: "academic_ShiftManagements",
			column: "TenantId");
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropIndex(
			name: "IX_academic_ShiftManagements_TenantId",
			table: "academic_ShiftManagements");
		migrationBuilder.DropColumn(name: "TenantId", table: "academic_ShiftManagements");
	}
}
