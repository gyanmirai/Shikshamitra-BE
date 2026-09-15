using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShikshaMitra.EntityFrameworkCore;

namespace ShikshaMitra.Migrations;

[DbContext(typeof(ShikshaMitraDbContext))]
[Migration("20260910180000_AddCanonicalStudentFeeAssignments")]
public class AddCanonicalStudentFeeAssignments : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.AlterColumn<Guid>("ConcessionPolicyId", "Finance_StudentFeeStructures", type: "uuid", nullable: true, oldClrType: typeof(Guid), oldType: "uuid");
		migrationBuilder.AlterColumn<Guid>("ConcessionPolicyVersionId", "Finance_StudentFeeStructures", type: "uuid", nullable: true, oldClrType: typeof(Guid), oldType: "uuid");
		migrationBuilder.AddColumn<string>("AssignmentSnapshotJson", "Finance_StudentFeeStructures", type: "text", nullable: true);
		migrationBuilder.AddColumn<Guid>("StudentFeeStructureId", "Finance_DueFees", type: "uuid", nullable: true);
		migrationBuilder.AddColumn<decimal>("AllocationPercentage", "Finance_DueFees", type: "numeric(18,6)", precision: 18, scale: 6, nullable: true);
		migrationBuilder.CreateIndex("IX_Finance_DueFees_StudentFeeStructureId", "Finance_DueFees", "StudentFeeStructureId");
		migrationBuilder.AddForeignKey("FK_DueFees_StudentFeeAssignment", "Finance_DueFees", "StudentFeeStructureId", "Finance_StudentFeeStructures", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
		// No historical amounts, concession evidence or unverifiable legacy links are rewritten.
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql("""
			DO $$ BEGIN
			IF EXISTS (SELECT 1 FROM "Finance_StudentFeeStructures" WHERE "AssignmentSnapshotJson" IS NOT NULL OR "ConcessionPolicyId" IS NULL OR "ConcessionPolicyVersionId" IS NULL)
			THEN RAISE EXCEPTION 'Canonical assignments exist. Preserve evidence and deploy a forward fix instead of downgrading.';
			END IF;
			END $$;
			""");
		migrationBuilder.DropForeignKey("FK_DueFees_StudentFeeAssignment", "Finance_DueFees");
		migrationBuilder.DropIndex("IX_Finance_DueFees_StudentFeeStructureId", "Finance_DueFees");
		migrationBuilder.DropColumn("StudentFeeStructureId", "Finance_DueFees");
		migrationBuilder.DropColumn("AllocationPercentage", "Finance_DueFees");
		migrationBuilder.DropColumn("AssignmentSnapshotJson", "Finance_StudentFeeStructures");
		migrationBuilder.AlterColumn<Guid>("ConcessionPolicyId", "Finance_StudentFeeStructures", type: "uuid", nullable: false, oldClrType: typeof(Guid), oldType: "uuid", oldNullable: true);
		migrationBuilder.AlterColumn<Guid>("ConcessionPolicyVersionId", "Finance_StudentFeeStructures", type: "uuid", nullable: false, oldClrType: typeof(Guid), oldType: "uuid", oldNullable: true);
	}
}
