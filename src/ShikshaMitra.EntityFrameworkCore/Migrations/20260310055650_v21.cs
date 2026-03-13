using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v21 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ConcessionTypes_AppAcademicYears_AcademicYearId",
                table: "ConcessionTypes");

            migrationBuilder.DropTable(
                name: "ConcessionTypeFeeHeads");

            migrationBuilder.DropIndex(
                name: "IX_ConcessionTypes_AcademicYearId",
                table: "ConcessionTypes");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "ConcessionTypes");

            migrationBuilder.DropColumn(
                name: "IsDocumentRequired",
                table: "ConcessionTypes");

            migrationBuilder.DropColumn(
                name: "AcceptedFormats",
                table: "ConcessionTypeDocuments");

            migrationBuilder.DropColumn(
                name: "AcademicYear",
                table: "ConcessionRequests");

            migrationBuilder.DropColumn(
                name: "BoardId",
                table: "ConcessionRequests");

            migrationBuilder.DropColumn(
                name: "DivisionId",
                table: "ConcessionRequests");

            migrationBuilder.DropColumn(
                name: "EducationMediumId",
                table: "ConcessionRequests");

            migrationBuilder.DropColumn(
                name: "StandardId",
                table: "ConcessionRequests");

            migrationBuilder.CreateTable(
                name: "AppConcessionTypeDocumentFormats",
                columns: table => new
                {
                    ConcessionTypeDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentType = table.Column<int>(type: "integer", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppConcessionTypeDocumentFormats", x => x.ConcessionTypeDocumentId);
                    table.ForeignKey(
                        name: "FK_AppConcessionTypeDocumentFormats_ConcessionTypeDocuments_Co~",
                        column: x => x.ConcessionTypeDocumentId,
                        principalTable: "ConcessionTypeDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppConcessionTypeDocumentFormats");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "ConcessionTypes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDocumentRequired",
                table: "ConcessionTypes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "AcceptedFormats",
                table: "ConcessionTypeDocuments",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AcademicYear",
                table: "ConcessionRequests",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "BoardId",
                table: "ConcessionRequests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "DivisionId",
                table: "ConcessionRequests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "EducationMediumId",
                table: "ConcessionRequests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "StandardId",
                table: "ConcessionRequests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "ConcessionTypeFeeHeads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConcessionTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeeHeadId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConcessionTypeFeeHeads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConcessionTypeFeeHeads_ConcessionTypes_ConcessionTypeId",
                        column: x => x.ConcessionTypeId,
                        principalTable: "ConcessionTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConcessionTypes_AcademicYearId",
                table: "ConcessionTypes",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_ConcessionTypeFeeHeads_ConcessionTypeId",
                table: "ConcessionTypeFeeHeads",
                column: "ConcessionTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_ConcessionTypes_AppAcademicYears_AcademicYearId",
                table: "ConcessionTypes",
                column: "AcademicYearId",
                principalTable: "AppAcademicYears",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
