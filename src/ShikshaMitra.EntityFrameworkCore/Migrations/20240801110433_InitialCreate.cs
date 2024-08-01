using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppStandards_TenantId",
                table: "AppStandards");

            migrationBuilder.DropIndex(
                name: "IX_AppDivisions_TenantId",
                table: "AppDivisions");

            migrationBuilder.AddColumn<Guid>(
                name: "AcademicYearId",
                table: "AppStandards",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "EducationBoardId",
                table: "AppStandards",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "EducationBoardStageId",
                table: "AppStandards",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "AppEducationBoards",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "AppEducationalMediums",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppAcademicYears",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppAcademicYears", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppEducationBoardStages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    EducationBoardId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppEducationBoardStages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppEducationBoardStages_AppEducationBoards_EducationBoardId",
                        column: x => x.EducationBoardId,
                        principalTable: "AppEducationBoards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppStandards_AcademicYearId",
                table: "AppStandards",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStandards_EducationBoardId",
                table: "AppStandards",
                column: "EducationBoardId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStandards_EducationBoardStageId",
                table: "AppStandards",
                column: "EducationBoardStageId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStandards_TenantId_AcademicYearId_EducationBoardId_Educa~",
                table: "AppStandards",
                columns: new[] { "TenantId", "AcademicYearId", "EducationBoardId", "EducationBoardStageId", "EducationalMediumId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppAcademicYears_TenantId_StartDate_EndDate",
                table: "AppAcademicYears",
                columns: new[] { "TenantId", "StartDate", "EndDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppEducationBoardStages_EducationBoardId",
                table: "AppEducationBoardStages",
                column: "EducationBoardId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppStandards_AppAcademicYears_AcademicYearId",
                table: "AppStandards",
                column: "AcademicYearId",
                principalTable: "AppAcademicYears",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AppStandards_AppEducationBoardStages_EducationBoardStageId",
                table: "AppStandards",
                column: "EducationBoardStageId",
                principalTable: "AppEducationBoardStages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AppStandards_AppEducationBoards_EducationBoardId",
                table: "AppStandards",
                column: "EducationBoardId",
                principalTable: "AppEducationBoards",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppStandards_AppAcademicYears_AcademicYearId",
                table: "AppStandards");

            migrationBuilder.DropForeignKey(
                name: "FK_AppStandards_AppEducationBoardStages_EducationBoardStageId",
                table: "AppStandards");

            migrationBuilder.DropForeignKey(
                name: "FK_AppStandards_AppEducationBoards_EducationBoardId",
                table: "AppStandards");

            migrationBuilder.DropTable(
                name: "AppAcademicYears");

            migrationBuilder.DropTable(
                name: "AppEducationBoardStages");

            migrationBuilder.DropIndex(
                name: "IX_AppStandards_AcademicYearId",
                table: "AppStandards");

            migrationBuilder.DropIndex(
                name: "IX_AppStandards_EducationBoardId",
                table: "AppStandards");

            migrationBuilder.DropIndex(
                name: "IX_AppStandards_EducationBoardStageId",
                table: "AppStandards");

            migrationBuilder.DropIndex(
                name: "IX_AppStandards_TenantId_AcademicYearId_EducationBoardId_Educa~",
                table: "AppStandards");

            migrationBuilder.DropColumn(
                name: "AcademicYearId",
                table: "AppStandards");

            migrationBuilder.DropColumn(
                name: "EducationBoardId",
                table: "AppStandards");

            migrationBuilder.DropColumn(
                name: "EducationBoardStageId",
                table: "AppStandards");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "AppEducationBoards");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "AppEducationalMediums");

            migrationBuilder.CreateIndex(
                name: "IX_AppStandards_TenantId",
                table: "AppStandards",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppDivisions_TenantId",
                table: "AppDivisions",
                column: "TenantId");
        }
    }
}
