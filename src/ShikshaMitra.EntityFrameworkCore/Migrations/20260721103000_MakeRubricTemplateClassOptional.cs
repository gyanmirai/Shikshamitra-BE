using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShikshaMitra.EntityFrameworkCore;

namespace ShikshaMitra.Migrations;

[DbContext(typeof(ShikshaMitraDbContext))]
[Migration("20260721103000_MakeRubricTemplateClassOptional")]
public partial class MakeRubricTemplateClassOptional : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.AlterColumn<Guid>(
			name: "StandardId",
			table: "AppAssessmentRubrics",
			type: "uuid",
			nullable: true,
			oldClrType: typeof(Guid),
			oldType: "uuid");
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql("""
			UPDATE "AppAssessmentRubrics"
			SET "StandardId" = '00000000-0000-0000-0000-000000000000'
			WHERE "StandardId" IS NULL;
			""");

		migrationBuilder.AlterColumn<Guid>(
			name: "StandardId",
			table: "AppAssessmentRubrics",
			type: "uuid",
			nullable: false,
			oldClrType: typeof(Guid),
			oldType: "uuid",
			oldNullable: true);
	}
}
