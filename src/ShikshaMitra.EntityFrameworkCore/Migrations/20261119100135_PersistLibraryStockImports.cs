using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class PersistLibraryStockImports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppLibraryStockImportBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShelfId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PayloadFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RowCount = table.Column<int>(type: "integer", nullable: false),
                    NewTitleCount = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    SourceName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppLibraryStockImportBatches", x => x.Id);
                    table.UniqueConstraint("AK_AppLibraryStockImportBatches_TenantId_Id", x => new { x.TenantId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "AppLibraryStockImportRows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    RowNumber = table.Column<int>(type: "integer", nullable: false),
                    SourceRowId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NormalizedSourceRowId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BookId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    IsNewTitle = table.Column<bool>(type: "boolean", nullable: false),
                    CopyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccessionNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppLibraryStockImportRows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppLibraryStockImportRows_AppLibraryStockImportBatches_Tena~",
                        columns: x => new { x.TenantId, x.BatchId },
                        principalTable: "AppLibraryStockImportBatches",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppLibraryStockImportRows_AppLibraryStockReceipts_TenantId_~",
                        columns: x => new { x.TenantId, x.ReceiptId },
                        principalTable: "AppLibraryStockReceipts",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryStockImportBatches_TenantId_AcademicYearId_Import~",
                table: "AppLibraryStockImportBatches",
                columns: new[] { "TenantId", "AcademicYearId", "ImportedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryStockImportBatches_TenantId_ImportedBy_Idempotenc~",
                table: "AppLibraryStockImportBatches",
                columns: new[] { "TenantId", "ImportedBy", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryStockImportRows_CopyId",
                table: "AppLibraryStockImportRows",
                column: "CopyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryStockImportRows_TenantId_BatchId_RowNumber",
                table: "AppLibraryStockImportRows",
                columns: new[] { "TenantId", "BatchId", "RowNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryStockImportRows_TenantId_NormalizedSourceRowId",
                table: "AppLibraryStockImportRows",
                columns: new[] { "TenantId", "NormalizedSourceRowId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryStockImportRows_TenantId_ReceiptId",
                table: "AppLibraryStockImportRows",
                columns: new[] { "TenantId", "ReceiptId" });
            AddEvidenceGuards(migrationBuilder);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            PreventEvidenceLoss(migrationBuilder);
            migrationBuilder.DropTable(
                name: "AppLibraryStockImportRows");

            migrationBuilder.DropTable(
                name: "AppLibraryStockImportBatches");
            migrationBuilder.Sql("""
                DROP FUNCTION sm_check_library_stock_import_row();
                DROP FUNCTION sm_check_library_stock_import_batch();
                DROP FUNCTION sm_keep_library_stock_import_evidence();
                """);
        }
    }
}
