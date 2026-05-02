using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v_SubmissionHomeworkIdNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "HomeworkAssignmentId",
                table: "AppStudentHomeworkAssignmentSubmissions",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateIndex(
                name: "IX_FeeStructureDetails_FeeHeadId",
                table: "FeeStructureDetails",
                column: "FeeHeadId");

            migrationBuilder.CreateIndex(
                name: "IX_FeeHeads_FeeCategoryId",
                table: "FeeHeads",
                column: "FeeCategoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_FeeHeads_FeeCategories_FeeCategoryId",
                table: "FeeHeads",
                column: "FeeCategoryId",
                principalTable: "FeeCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FeeStructureDetails_FeeHeads_FeeHeadId",
                table: "FeeStructureDetails",
                column: "FeeHeadId",
                principalTable: "FeeHeads",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FeeHeads_FeeCategories_FeeCategoryId",
                table: "FeeHeads");

            migrationBuilder.DropForeignKey(
                name: "FK_FeeStructureDetails_FeeHeads_FeeHeadId",
                table: "FeeStructureDetails");

            migrationBuilder.DropIndex(
                name: "IX_FeeStructureDetails_FeeHeadId",
                table: "FeeStructureDetails");

            migrationBuilder.DropIndex(
                name: "IX_FeeHeads_FeeCategoryId",
                table: "FeeHeads");

            migrationBuilder.AlterColumn<Guid>(
                name: "HomeworkAssignmentId",
                table: "AppStudentHomeworkAssignmentSubmissions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
