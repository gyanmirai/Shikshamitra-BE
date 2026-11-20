using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class PersistLibraryClassroomCustody : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppLibraryCustodyHandoffs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Custodian = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    StandardId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClassName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: true),
                    RoomName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Purpose = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    HandedOffOn = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpectedReturnOn = table.Column<DateOnly>(type: "date", nullable: true),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CopyCount = table.Column<int>(type: "integer", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PayloadFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppLibraryCustodyHandoffs", x => x.Id);
                    table.UniqueConstraint("AK_AppLibraryCustodyHandoffs_TenantId_Id", x => new { x.TenantId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "AppLibraryCustodyReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    HandoffId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_AppLibraryCustodyReceipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppLibraryCustodyReceipts_AppLibraryCustodyHandoffs_TenantI~",
                        columns: x => new { x.TenantId, x.HandoffId },
                        principalTable: "AppLibraryCustodyHandoffs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppLibraryCustodyItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    HandoffId = table.Column<Guid>(type: "uuid", nullable: false),
                    CopyId = table.Column<Guid>(type: "uuid", nullable: false),
                    BookId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    AccessionNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    HomeSectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    HomeShelfId = table.Column<Guid>(type: "uuid", nullable: false),
                    HandoffEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReturnReceiptId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReturnEventId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReturnedCondition = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppLibraryCustodyItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppLibraryCustodyItems_AppBookCopies_CopyId",
                        column: x => x.CopyId,
                        principalTable: "AppBookCopies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppLibraryCustodyItems_AppLibraryCopyEvents_HandoffEventId",
                        column: x => x.HandoffEventId,
                        principalTable: "AppLibraryCopyEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppLibraryCustodyItems_AppLibraryCopyEvents_ReturnEventId",
                        column: x => x.ReturnEventId,
                        principalTable: "AppLibraryCopyEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppLibraryCustodyItems_AppLibraryCustodyHandoffs_TenantId_H~",
                        columns: x => new { x.TenantId, x.HandoffId },
                        principalTable: "AppLibraryCustodyHandoffs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppLibraryCustodyItems_AppLibraryCustodyReceipts_ReturnRece~",
                        column: x => x.ReturnReceiptId,
                        principalTable: "AppLibraryCustodyReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryCustodyHandoffs_TenantId_ActorId_IdempotencyKey",
                table: "AppLibraryCustodyHandoffs",
                columns: new[] { "TenantId", "ActorId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryCustodyHandoffs_TenantId_RecordedAt_Id",
                table: "AppLibraryCustodyHandoffs",
                columns: new[] { "TenantId", "RecordedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryCustodyItems_CopyId",
                table: "AppLibraryCustodyItems",
                column: "CopyId");

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryCustodyItems_HandoffEventId",
                table: "AppLibraryCustodyItems",
                column: "HandoffEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryCustodyItems_ReturnEventId",
                table: "AppLibraryCustodyItems",
                column: "ReturnEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryCustodyItems_ReturnReceiptId",
                table: "AppLibraryCustodyItems",
                column: "ReturnReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryCustodyItems_TenantId_CopyId",
                table: "AppLibraryCustodyItems",
                columns: new[] { "TenantId", "CopyId" },
                unique: true,
                filter: "\"ReturnReceiptId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryCustodyItems_TenantId_HandoffId_CopyId",
                table: "AppLibraryCustodyItems",
                columns: new[] { "TenantId", "HandoffId", "CopyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryCustodyReceipts_TenantId_ActorId_IdempotencyKey",
                table: "AppLibraryCustodyReceipts",
                columns: new[] { "TenantId", "ActorId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryCustodyReceipts_TenantId_HandoffId",
                table: "AppLibraryCustodyReceipts",
                columns: new[] { "TenantId", "HandoffId" });
            migrationBuilder.DropCheckConstraint(
                name: "CK_LibraryStockResult_Finding",
                table: "AppLibraryStockVerificationResults");
            migrationBuilder.AddCheckConstraint(
                name: "CK_LibraryStockResult_Finding",
                table: "AppLibraryStockVerificationResults",
                sql: "\"Finding\" BETWEEN 1 AND 16");
            AddCustodyEvidenceGuards(migrationBuilder);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            RemoveCustodyCopyGuards(migrationBuilder);
            migrationBuilder.DropTable(
                name: "AppLibraryCustodyItems");

            migrationBuilder.DropTable(
                name: "AppLibraryCustodyReceipts");

            migrationBuilder.DropTable(
                name: "AppLibraryCustodyHandoffs");
            RemoveCustodyEvidenceFunctions(migrationBuilder);
            migrationBuilder.DropCheckConstraint(
                name: "CK_LibraryStockResult_Finding",
                table: "AppLibraryStockVerificationResults");
            migrationBuilder.AddCheckConstraint(
                name: "CK_LibraryStockResult_Finding",
                table: "AppLibraryStockVerificationResults",
                sql: "\"Finding\" BETWEEN 1 AND 15");
        }
    }
}
