using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v9 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "StandardFeeStructureId",
                table: "StudentFeeHeads",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptBooks_AcademicYearId",
                table: "ReceiptBooks",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptBooks_BoardId",
                table: "ReceiptBooks",
                column: "BoardId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptBooks_MediumId",
                table: "ReceiptBooks",
                column: "MediumId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptBooks_MediumStageId",
                table: "ReceiptBooks",
                column: "MediumStageId");

            migrationBuilder.AddForeignKey(
                name: "FK_ReceiptBooks_AppAcademicYears_AcademicYearId",
                table: "ReceiptBooks",
                column: "AcademicYearId",
                principalTable: "AppAcademicYears",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ReceiptBooks_AppEducationBoards_BoardId",
                table: "ReceiptBooks",
                column: "BoardId",
                principalTable: "AppEducationBoards",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ReceiptBooks_AppEducationMediumStages_MediumStageId",
                table: "ReceiptBooks",
                column: "MediumStageId",
                principalTable: "AppEducationMediumStages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ReceiptBooks_AppEducationMediums_MediumId",
                table: "ReceiptBooks",
                column: "MediumId",
                principalTable: "AppEducationMediums",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReceiptBooks_AppAcademicYears_AcademicYearId",
                table: "ReceiptBooks");

            migrationBuilder.DropForeignKey(
                name: "FK_ReceiptBooks_AppEducationBoards_BoardId",
                table: "ReceiptBooks");

            migrationBuilder.DropForeignKey(
                name: "FK_ReceiptBooks_AppEducationMediumStages_MediumStageId",
                table: "ReceiptBooks");

            migrationBuilder.DropForeignKey(
                name: "FK_ReceiptBooks_AppEducationMediums_MediumId",
                table: "ReceiptBooks");

            migrationBuilder.DropIndex(
                name: "IX_ReceiptBooks_AcademicYearId",
                table: "ReceiptBooks");

            migrationBuilder.DropIndex(
                name: "IX_ReceiptBooks_BoardId",
                table: "ReceiptBooks");

            migrationBuilder.DropIndex(
                name: "IX_ReceiptBooks_MediumId",
                table: "ReceiptBooks");

            migrationBuilder.DropIndex(
                name: "IX_ReceiptBooks_MediumStageId",
                table: "ReceiptBooks");

            migrationBuilder.DropColumn(
                name: "StandardFeeStructureId",
                table: "StudentFeeHeads");
        }
    }
}
