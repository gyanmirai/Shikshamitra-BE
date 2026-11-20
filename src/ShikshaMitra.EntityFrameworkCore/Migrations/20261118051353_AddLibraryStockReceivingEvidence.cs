using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddLibraryStockReceivingEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppLibraryStockReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceivedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    SourceName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Reference = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    RecordedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpectedLineCount = table.Column<int>(type: "integer", nullable: false),
                    ExpectedCopyCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppLibraryStockReceipts", x => x.Id);
                    table.UniqueConstraint("AK_AppLibraryStockReceipts_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_LibraryStockReceipts_Counts", "\"ExpectedLineCount\" BETWEEN 1 AND 100 AND \"ExpectedCopyCount\" BETWEEN 1 AND 5000");
                    table.CheckConstraint("CK_LibraryStockReceipts_Source", "\"Source\" IN (0, 1, 2, 3)");
                });

            migrationBuilder.CreateTable(
                name: "AppLibraryStockReceiptCommands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PayloadFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppLibraryStockReceiptCommands", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppLibraryStockReceiptCommands_AppLibraryStockReceipts_Tena~",
                        columns: x => new { x.TenantId, x.ReceiptId },
                        principalTable: "AppLibraryStockReceipts",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppLibraryStockReceiptLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    BookId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShelfId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpectedCopyCount = table.Column<int>(type: "integer", nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric", nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppLibraryStockReceiptLines", x => x.Id);
                    table.UniqueConstraint("AK_AppLibraryStockReceiptLines_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_LibraryStockReceiptLines_Cost", "(\"UnitCost\" IS NULL AND \"Currency\" IS NULL) OR (\"UnitCost\" IS NOT NULL AND \"Currency\" IS NOT NULL AND length(\"Currency\") = 3 AND \"UnitCost\" >= 0 AND \"UnitCost\" <= 9999999999999999.99 AND \"UnitCost\" = round(\"UnitCost\", 2))");
                    table.CheckConstraint("CK_LibraryStockReceiptLines_Count", "\"ExpectedCopyCount\" BETWEEN 1 AND 5000");
                    table.CheckConstraint("CK_LibraryStockReceiptLines_Number", "\"LineNumber\" BETWEEN 1 AND 100");
                    table.ForeignKey(
                        name: "FK_AppLibraryStockReceiptLines_AppBooks_BookId",
                        column: x => x.BookId,
                        principalTable: "AppBooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppLibraryStockReceiptLines_AppLibrarySections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "AppLibrarySections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppLibraryStockReceiptLines_AppLibraryShelves_ShelfId",
                        column: x => x.ShelfId,
                        principalTable: "AppLibraryShelves",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppLibraryStockReceiptLines_AppLibraryStockReceipts_TenantI~",
                        columns: x => new { x.TenantId, x.ReceiptId },
                        principalTable: "AppLibraryStockReceipts",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppLibraryReceivedCopies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceiptLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    CopyId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccessionNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CopyNumber = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppLibraryReceivedCopies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppLibraryReceivedCopies_AppBookCopies_CopyId",
                        column: x => x.CopyId,
                        principalTable: "AppBookCopies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppLibraryReceivedCopies_AppLibraryStockReceiptLines_Tenant~",
                        columns: x => new { x.TenantId, x.ReceiptLineId },
                        principalTable: "AppLibraryStockReceiptLines",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryReceivedCopies_CopyId",
                table: "AppLibraryReceivedCopies",
                column: "CopyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryReceivedCopies_TenantId_ReceiptLineId",
                table: "AppLibraryReceivedCopies",
                columns: new[] { "TenantId", "ReceiptLineId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryStockReceiptCommands_ReceiptId",
                table: "AppLibraryStockReceiptCommands",
                column: "ReceiptId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryStockReceiptCommands_TenantId_ActorId_Idempotency~",
                table: "AppLibraryStockReceiptCommands",
                columns: new[] { "TenantId", "ActorId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryStockReceiptCommands_TenantId_ReceiptId",
                table: "AppLibraryStockReceiptCommands",
                columns: new[] { "TenantId", "ReceiptId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryStockReceiptLines_BookId",
                table: "AppLibraryStockReceiptLines",
                column: "BookId");

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryStockReceiptLines_ReceiptId_LineNumber",
                table: "AppLibraryStockReceiptLines",
                columns: new[] { "ReceiptId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryStockReceiptLines_SectionId",
                table: "AppLibraryStockReceiptLines",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryStockReceiptLines_ShelfId",
                table: "AppLibraryStockReceiptLines",
                column: "ShelfId");

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryStockReceiptLines_TenantId_ReceiptId",
                table: "AppLibraryStockReceiptLines",
                columns: new[] { "TenantId", "ReceiptId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryStockReceipts_TenantId_ReceivedOn_Id",
                table: "AppLibraryStockReceipts",
                columns: new[] { "TenantId", "ReceivedOn", "Id" });

            InstallReceivingSourceGuards(migrationBuilder);
            InstallReceivingSeal(migrationBuilder);
            InstallReceivingRetention(migrationBuilder);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            EnsureReceivingRollbackSafe(migrationBuilder);
            migrationBuilder.DropTable(
                name: "AppLibraryReceivedCopies");

            migrationBuilder.DropTable(
                name: "AppLibraryStockReceiptCommands");

            migrationBuilder.DropTable(
                name: "AppLibraryStockReceiptLines");

            migrationBuilder.DropTable(
                name: "AppLibraryStockReceipts");

            RemoveReceivingGuardFunctions(migrationBuilder);
        }
    }
}
