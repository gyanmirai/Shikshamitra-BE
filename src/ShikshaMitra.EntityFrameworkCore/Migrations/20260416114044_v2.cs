using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FeeStructureDetails_FeeStructureStandards_FeeStructureStand~",
                table: "FeeStructureDetails");

            migrationBuilder.DropTable(
                name: "FeeStructureStandards");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "FeeStructures");

            migrationBuilder.RenameColumn(
                name: "MediumId",
                table: "FeeStructures",
                newName: "StandardId");

            migrationBuilder.RenameColumn(
                name: "BoardId",
                table: "FeeStructures",
                newName: "FeeStructureMetadataId");

            migrationBuilder.RenameColumn(
                name: "FeeStructureStandardId",
                table: "FeeStructureDetails",
                newName: "FeeStructureId");

            migrationBuilder.RenameIndex(
                name: "IX_FeeStructureDetails_FeeStructureStandardId",
                table: "FeeStructureDetails",
                newName: "IX_FeeStructureDetails_FeeStructureId");

            migrationBuilder.AddColumn<Guid>(
                name: "AcademicBoardId",
                table: "FeeStructures",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "AcademicMediumId",
                table: "FeeStructures",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<double>(
                name: "Amount",
                table: "FeeStructureDetails",
                type: "double precision",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.CreateTable(
                name: "FeeStructureMetadatas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicBoardId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicMediumId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReceiptBookId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
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
                    table.PrimaryKey("PK_FeeStructureMetadatas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeeStructureMetadatas_AppAcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AppAcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FeeStructureMetadatas_AppEducationBoards_AcademicBoardId",
                        column: x => x.AcademicBoardId,
                        principalTable: "AppEducationBoards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FeeStructureMetadatas_AppEducationMediums_AcademicMediumId",
                        column: x => x.AcademicMediumId,
                        principalTable: "AppEducationMediums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FeeStructureMetadatas_ReceiptBooks_ReceiptBookId",
                        column: x => x.ReceiptBookId,
                        principalTable: "ReceiptBooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FeeStructureMetadatas_AcademicBoardId",
                table: "FeeStructureMetadatas",
                column: "AcademicBoardId");

            migrationBuilder.CreateIndex(
                name: "IX_FeeStructureMetadatas_AcademicMediumId",
                table: "FeeStructureMetadatas",
                column: "AcademicMediumId");

            migrationBuilder.CreateIndex(
                name: "IX_FeeStructureMetadatas_AcademicYearId",
                table: "FeeStructureMetadatas",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_FeeStructureMetadatas_ReceiptBookId",
                table: "FeeStructureMetadatas",
                column: "ReceiptBookId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FeeStructureDetails_FeeStructures_FeeStructureId",
                table: "FeeStructureDetails");

            migrationBuilder.DropTable(
                name: "FeeStructureMetadatas");

            migrationBuilder.DropColumn(
                name: "AcademicBoardId",
                table: "FeeStructures");

            migrationBuilder.DropColumn(
                name: "AcademicMediumId",
                table: "FeeStructures");

            migrationBuilder.RenameColumn(
                name: "StandardId",
                table: "FeeStructures",
                newName: "MediumId");

            migrationBuilder.RenameColumn(
                name: "FeeStructureMetadataId",
                table: "FeeStructures",
                newName: "BoardId");

            migrationBuilder.RenameColumn(
                name: "FeeStructureId",
                table: "FeeStructureDetails",
                newName: "FeeStructureStandardId");

            migrationBuilder.RenameIndex(
                name: "IX_FeeStructureDetails_FeeStructureId",
                table: "FeeStructureDetails",
                newName: "IX_FeeStructureDetails_FeeStructureStandardId");

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "FeeStructures",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "FeeStructureDetails",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "double precision");

            migrationBuilder.CreateTable(
                name: "FeeStructureStandards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FeeStructureId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false),
                    StandardId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeeStructureStandards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeeStructureStandards_FeeStructures_FeeStructureId",
                        column: x => x.FeeStructureId,
                        principalTable: "FeeStructures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FeeStructureStandards_FeeStructureId",
                table: "FeeStructureStandards",
                column: "FeeStructureId");

            migrationBuilder.AddForeignKey(
                name: "FK_FeeStructureDetails_FeeStructureStandards_FeeStructureStand~",
                table: "FeeStructureDetails",
                column: "FeeStructureStandardId",
                principalTable: "FeeStructureStandards",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
