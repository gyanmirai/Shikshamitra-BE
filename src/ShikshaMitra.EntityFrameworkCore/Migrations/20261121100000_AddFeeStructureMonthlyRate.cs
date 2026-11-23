using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShikshaMitra.EntityFrameworkCore;

namespace ShikshaMitra.Migrations;

[DbContext(typeof(ShikshaMitraDbContext))]
[Migration("20261121100000_AddFeeStructureMonthlyRate")]
public class AddFeeStructureMonthlyRate : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.AddColumn<decimal>(
			name: "MonthlyRate",
			table: "Finance_FeeStructureDetails",
			schema: ShikshaMitraConsts.DbSchema,
			type: "numeric(18,2)",
			precision: 18,
			scale: 2,
			nullable: true);
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropColumn(
			name: "MonthlyRate",
			table: "Finance_FeeStructureDetails",
			schema: ShikshaMitraConsts.DbSchema);
	}
}
