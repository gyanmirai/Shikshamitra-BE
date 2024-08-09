using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class educationmediumstage1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppStandards_AppEducationMediumStages_EducationBoardStageId",
                table: "AppStandards");

            migrationBuilder.DropIndex(
                name: "IX_AppStandards_TenantId_AcademicYearId_EducationBoardId_Educa~",
                table: "AppStandards");

            migrationBuilder.RenameColumn(
                name: "EducationBoardStageId",
                table: "AppStandards",
                newName: "EducationMediumStageId");

            migrationBuilder.RenameIndex(
                name: "IX_AppStandards_EducationBoardStageId",
                table: "AppStandards",
                newName: "IX_AppStandards_EducationMediumStageId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStandards_TenantId_AcademicYearId_EducationBoardId_Educa~",
                table: "AppStandards",
                columns: new[] { "TenantId", "AcademicYearId", "EducationBoardId", "EducationMediumId", "EducationMediumStageId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AppStandards_AppEducationMediumStages_EducationMediumStageId",
                table: "AppStandards",
                column: "EducationMediumStageId",
                principalTable: "AppEducationMediumStages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppStandards_AppEducationMediumStages_EducationMediumStageId",
                table: "AppStandards");

            migrationBuilder.DropIndex(
                name: "IX_AppStandards_TenantId_AcademicYearId_EducationBoardId_Educa~",
                table: "AppStandards");

            migrationBuilder.RenameColumn(
                name: "EducationMediumStageId",
                table: "AppStandards",
                newName: "EducationBoardStageId");

            migrationBuilder.RenameIndex(
                name: "IX_AppStandards_EducationMediumStageId",
                table: "AppStandards",
                newName: "IX_AppStandards_EducationBoardStageId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStandards_TenantId_AcademicYearId_EducationBoardId_Educa~",
                table: "AppStandards",
                columns: new[] { "TenantId", "AcademicYearId", "EducationBoardId", "EducationBoardStageId", "EducationMediumId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AppStandards_AppEducationMediumStages_EducationBoardStageId",
                table: "AppStandards",
                column: "EducationBoardStageId",
                principalTable: "AppEducationMediumStages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
