using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class Added_ConcessionType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConcessionTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    BasedOn = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    IsDocumentRequired = table.Column<bool>(type: "boolean", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_ConcessionTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConcessionTypes_AppAcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AppAcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConcessionTypeDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConcessionTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentName = table.Column<string>(type: "text", nullable: false),
                    DocumentSize = table.Column<long>(type: "bigint", nullable: false),
                    AcceptedFormats = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConcessionTypeDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConcessionTypeDocuments_ConcessionTypes_ConcessionTypeId",
                        column: x => x.ConcessionTypeId,
                        principalTable: "ConcessionTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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
                name: "IX_ConcessionTypeDocuments_ConcessionTypeId",
                table: "ConcessionTypeDocuments",
                column: "ConcessionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ConcessionTypeFeeHeads_ConcessionTypeId",
                table: "ConcessionTypeFeeHeads",
                column: "ConcessionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ConcessionTypes_AcademicYearId",
                table: "ConcessionTypes",
                column: "AcademicYearId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConcessionTypeDocuments");

            migrationBuilder.DropTable(
                name: "ConcessionTypeFeeHeads");

            migrationBuilder.DropTable(
                name: "ConcessionTypes");
        }
    }
}
