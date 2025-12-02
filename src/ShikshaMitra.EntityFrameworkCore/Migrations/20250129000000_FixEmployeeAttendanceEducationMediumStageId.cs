using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class FixEmployeeAttendanceEducationMediumStageId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Make EducationMediumStageId nullable if it was previously NOT NULL
            migrationBuilder.AlterColumn<Guid>(
                name: "EducationMediumStageId",
                table: "AppEmployeeAttendances",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: false,
                oldDefaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Step 2: Backfill EducationMediumStageId from Employee table for existing records
            // This SQL updates all attendance records where EducationMediumStageId is NULL or Guid.Empty
            // by copying the value from the related Employee record
            migrationBuilder.Sql(@"
                UPDATE ""AppEmployeeAttendances"" ea
                SET ""EducationMediumStageId"" = e.""EducationMediumStageId""
                FROM ""AppEmployees"" e
                WHERE ea.""EmployeeId"" = e.""Id""
                  AND (ea.""EducationMediumStageId"" IS NULL 
                       OR ea.""EducationMediumStageId"" = '00000000-0000-0000-0000-000000000000'::uuid);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverting: Make EducationMediumStageId non-nullable again with Guid.Empty default
            migrationBuilder.AlterColumn<Guid>(
                name: "EducationMediumStageId",
                table: "AppEmployeeAttendances",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
