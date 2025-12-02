using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v39 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "EducationMediumStageId",
                table: "AppEmployeeAttendances",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EmployeeAttendance_EducationMediumStageId_Consistency",
                table: "AppEmployeeAttendances",
                sql: "\"EducationMediumStageId\" IS NULL OR \n			  \"EducationMediumStageId\" = (\n				  SELECT \"EducationMediumStageId\" \n				  FROM \"AppEmployees\" \n				  WHERE \"Id\" = \"EmployeeId\"\n			  )\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_EmployeeAttendance_EducationMediumStageId_Consistency",
                table: "AppEmployeeAttendances");

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
