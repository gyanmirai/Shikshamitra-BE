using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class PersistLibraryCampusTransfers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_LibraryStockResult_Finding",
                table: "AppLibraryStockVerificationResults");

            migrationBuilder.CreateTable(
                name: "AppLibraryCampusTransfers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceCampusId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceCampus = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    SourceSectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceSection = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DestinationCampusId = table.Column<Guid>(type: "uuid", nullable: false),
                    DestinationCampus = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    DestinationSectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DestinationSection = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DestinationShelfId = table.Column<Guid>(type: "uuid", nullable: false),
                    DestinationShelf = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DispatchedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CopyCount = table.Column<int>(type: "integer", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PayloadFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppLibraryCampusTransfers", x => x.Id);
                    table.UniqueConstraint("AK_AppLibraryCampusTransfers_TenantId_Id", x => new { x.TenantId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "AppLibraryCampusTransferReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransferId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceivedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CopyCount = table.Column<int>(type: "integer", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PayloadFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppLibraryCampusTransferReceipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppLibraryCampusTransferReceipts_AppLibraryCampusTransfers_~",
                        columns: x => new { x.TenantId, x.TransferId },
                        principalTable: "AppLibraryCampusTransfers",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppLibraryCampusTransferItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransferId = table.Column<Guid>(type: "uuid", nullable: false),
                    CopyId = table.Column<Guid>(type: "uuid", nullable: false),
                    BookId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    AccessionNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SourceShelfId = table.Column<Guid>(type: "uuid", nullable: false),
                    DispatchEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceiptId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReceiptEventId = table.Column<Guid>(type: "uuid", nullable: true),
                    Condition = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppLibraryCampusTransferItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppLibraryCampusTransferItems_AppBookCopies_CopyId",
                        column: x => x.CopyId,
                        principalTable: "AppBookCopies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppLibraryCampusTransferItems_AppLibraryCampusTransferRecei~",
                        column: x => x.ReceiptId,
                        principalTable: "AppLibraryCampusTransferReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppLibraryCampusTransferItems_AppLibraryCampusTransfers_Ten~",
                        columns: x => new { x.TenantId, x.TransferId },
                        principalTable: "AppLibraryCampusTransfers",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppLibraryCampusTransferItems_AppLibraryCopyEvents_Dispatch~",
                        column: x => x.DispatchEventId,
                        principalTable: "AppLibraryCopyEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppLibraryCampusTransferItems_AppLibraryCopyEvents_ReceiptE~",
                        column: x => x.ReceiptEventId,
                        principalTable: "AppLibraryCopyEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_LibraryStockResult_Finding",
                table: "AppLibraryStockVerificationResults",
                sql: "\"Finding\" BETWEEN 1 AND 17");

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryCampusTransferItems_CopyId",
                table: "AppLibraryCampusTransferItems",
                column: "CopyId");

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryCampusTransferItems_DispatchEventId",
                table: "AppLibraryCampusTransferItems",
                column: "DispatchEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryCampusTransferItems_ReceiptEventId",
                table: "AppLibraryCampusTransferItems",
                column: "ReceiptEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryCampusTransferItems_ReceiptId",
                table: "AppLibraryCampusTransferItems",
                column: "ReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryCampusTransferItems_TenantId_CopyId",
                table: "AppLibraryCampusTransferItems",
                columns: new[] { "TenantId", "CopyId" },
                unique: true,
                filter: "\"ReceiptId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryCampusTransferItems_TenantId_TransferId_CopyId",
                table: "AppLibraryCampusTransferItems",
                columns: new[] { "TenantId", "TransferId", "CopyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryCampusTransferReceipts_TenantId_TransferId_ActorI~",
                table: "AppLibraryCampusTransferReceipts",
                columns: new[] { "TenantId", "TransferId", "ActorId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryCampusTransfers_TenantId_ActorId_IdempotencyKey",
                table: "AppLibraryCampusTransfers",
                columns: new[] { "TenantId", "ActorId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryCampusTransfers_TenantId_DestinationSectionId",
                table: "AppLibraryCampusTransfers",
                columns: new[] { "TenantId", "DestinationSectionId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryCampusTransfers_TenantId_RecordedAt_Id",
                table: "AppLibraryCampusTransfers",
                columns: new[] { "TenantId", "RecordedAt", "Id" });

            AddTransferEvidenceGuards(migrationBuilder);
            AddTransferInventoryGuards(migrationBuilder);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            RemoveTransferGuards(migrationBuilder);
            migrationBuilder.DropTable(
                name: "AppLibraryCampusTransferItems");

            migrationBuilder.DropTable(
                name: "AppLibraryCampusTransferReceipts");

            migrationBuilder.DropTable(
                name: "AppLibraryCampusTransfers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LibraryStockResult_Finding",
                table: "AppLibraryStockVerificationResults");

            migrationBuilder.AddCheckConstraint(
                name: "CK_LibraryStockResult_Finding",
                table: "AppLibraryStockVerificationResults",
                sql: "\"Finding\" BETWEEN 1 AND 16");
        }
    }
}
