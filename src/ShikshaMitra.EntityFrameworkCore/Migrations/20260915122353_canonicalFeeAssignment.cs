using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddCanonicalStudentFeeAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "ConcessionPolicyVersionId",
                table: "Finance_StudentFeeStructures",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "ConcessionPolicyId",
                table: "Finance_StudentFeeStructures",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "AssignmentSnapshotJson",
                table: "Finance_StudentFeeStructures",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AllocationPercentage",
                table: "Finance_DueFees",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StudentFeeStructureId",
                table: "Finance_DueFees",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Finance_DueFees_StudentFeeStructureId",
                table: "Finance_DueFees",
                column: "StudentFeeStructureId");

            migrationBuilder.AddForeignKey(
                name: "FK_DueFees_StudentFeeAssignment",
                table: "Finance_DueFees",
                column: "StudentFeeStructureId",
                principalTable: "Finance_StudentFeeStructures",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Keep the evidence check and schema rollback in the same migration transaction.
            migrationBuilder.Sql("""
                LOCK TABLE "Finance_StudentFeeStructures", "Finance_DueFees" IN ACCESS EXCLUSIVE MODE;
                DO $rollback$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM "Finance_StudentFeeStructures"
                        WHERE "AssignmentSnapshotJson" IS NOT NULL
                           OR "ConcessionPolicyId" IS NULL
                           OR "ConcessionPolicyVersionId" IS NULL
                    ) OR EXISTS (
                        SELECT 1 FROM "Finance_DueFees"
                        WHERE "StudentFeeStructureId" IS NOT NULL
                           OR "AllocationPercentage" IS NOT NULL
                    ) THEN
                        RAISE EXCEPTION 'Canonical assignment evidence exists; use a forward fix instead of rolling back AddCanonicalStudentFeeAssignments.';
                    END IF;
                END $rollback$;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_DueFees_StudentFeeAssignment",
                table: "Finance_DueFees");

            migrationBuilder.DropIndex(
                name: "IX_Finance_DueFees_StudentFeeStructureId",
                table: "Finance_DueFees");

            migrationBuilder.DropColumn(
                name: "AssignmentSnapshotJson",
                table: "Finance_StudentFeeStructures");

            migrationBuilder.DropColumn(
                name: "AllocationPercentage",
                table: "Finance_DueFees");

            migrationBuilder.DropColumn(
                name: "StudentFeeStructureId",
                table: "Finance_DueFees");

            migrationBuilder.AlterColumn<Guid>(
                name: "ConcessionPolicyVersionId",
                table: "Finance_StudentFeeStructures",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ConcessionPolicyId",
                table: "Finance_StudentFeeStructures",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
