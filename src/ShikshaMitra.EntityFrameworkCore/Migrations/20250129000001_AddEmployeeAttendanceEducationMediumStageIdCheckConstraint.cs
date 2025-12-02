using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
	/// <inheritdoc />
	public partial class AddEmployeeAttendanceEducationMediumStageIdCheckConstraint : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			// Drop the invalid CHECK constraint if it exists
			// PostgreSQL CHECK constraints cannot reference other tables via subqueries
			// This constraint was incorrectly created and needs to be removed
			migrationBuilder.Sql(@"
				DO $$ 
				BEGIN
					IF EXISTS (
						SELECT 1 
						FROM pg_constraint 
						WHERE conname = 'CK_EmployeeAttendance_EducationMediumStageId_Consistency'
					) THEN
						ALTER TABLE ""AppEmployeeAttendances"" 
						DROP CONSTRAINT ""CK_EmployeeAttendance_EducationMediumStageId_Consistency"";
					END IF;
				END $$;
			");
			
			// Note: EducationMediumStageId consistency is now enforced in the application layer
			// via EmployeeAttendanceAppService.ValidateAndNormalizeEducationMediumStageIdAsync()
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			// No action needed for rollback
			// The constraint was invalid and should not be recreated
		}
	}
}
