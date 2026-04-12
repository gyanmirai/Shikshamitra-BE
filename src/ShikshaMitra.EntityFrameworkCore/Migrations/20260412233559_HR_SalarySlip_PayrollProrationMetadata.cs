using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations;

/// <inheritdoc />
public partial class HR_SalarySlip_PayrollProrationMetadata : Migration
{
	/// <inheritdoc />
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.AddColumn<decimal>(
			name: "PayableDays",
			table: "HR_SalarySlips",
			type: "numeric(18,2)",
			precision: 18,
			scale: 2,
			nullable: true);

		migrationBuilder.AddColumn<int>(
			name: "PayrollBasisDays",
			table: "HR_SalarySlips",
			type: "integer",
			nullable: true);

		migrationBuilder.AddColumn<int>(
			name: "PayrollDayCalculationMode",
			table: "HR_SalarySlips",
			type: "integer",
			nullable: true);

		migrationBuilder.AddColumn<decimal>(
			name: "ProrationFactor",
			table: "HR_SalarySlips",
			type: "numeric(18,4)",
			precision: 18,
			scale: 4,
			nullable: true);
	}

	/// <inheritdoc />
	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropColumn(
			name: "PayableDays",
			table: "HR_SalarySlips");

		migrationBuilder.DropColumn(
			name: "PayrollBasisDays",
			table: "HR_SalarySlips");

		migrationBuilder.DropColumn(
			name: "PayrollDayCalculationMode",
			table: "HR_SalarySlips");

		migrationBuilder.DropColumn(
			name: "ProrationFactor",
			table: "HR_SalarySlips");
	}
}
