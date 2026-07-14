using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
	/// <inheritdoc />
	public partial class AddSeatingPlanSpecialRequirements : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql("""
                ALTER TABLE IF EXISTS "AppExamSeatingPlans"
                    ADD COLUMN IF NOT EXISTS "SpecialRequirementStudentIdsCsv" character varying(4000);
                """);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql("""
                ALTER TABLE IF EXISTS "AppExamSeatingPlans"
                    DROP COLUMN IF EXISTS "SpecialRequirementStudentIdsCsv";
                """);
		}
	}
}
