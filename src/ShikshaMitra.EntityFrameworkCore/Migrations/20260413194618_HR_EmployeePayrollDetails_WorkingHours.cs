using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations;

/// <inheritdoc />
public partial class HR_EmployeePayrollDetails_WorkingHours : Migration
{
	/// <inheritdoc />
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropColumn(
			name: "BasicSalary",
			table: "AppEmployeePayrollDetails");

		migrationBuilder.DropColumn(
			name: "TdsPercentage",
			table: "AppEmployeePayrollDetails");

		migrationBuilder.DropColumn(
			name: "TdsAmount",
			table: "AppEmployeePayrollDetails");

		migrationBuilder.AddColumn<decimal>(
			name: "TotalWorkingHourPerMonth",
			table: "AppEmployeePayrollDetails",
			type: "decimal(18,2)",
			nullable: true);
	}

	/// <inheritdoc />
	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropColumn(
			name: "TotalWorkingHourPerMonth",
			table: "AppEmployeePayrollDetails");

		migrationBuilder.AddColumn<decimal>(
			name: "BasicSalary",
			table: "AppEmployeePayrollDetails",
			type: "numeric(18,2)",
			nullable: true);

		migrationBuilder.AddColumn<decimal>(
			name: "TdsPercentage",
			table: "AppEmployeePayrollDetails",
			type: "numeric(5,2)",
			nullable: true);

		migrationBuilder.AddColumn<decimal>(
			name: "TdsAmount",
			table: "AppEmployeePayrollDetails",
			type: "numeric(18,2)",
			nullable: true);
	}
}
