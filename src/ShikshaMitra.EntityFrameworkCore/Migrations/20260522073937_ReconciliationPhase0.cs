using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class ReconciliationPhase0 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FeeCollectionDueFeeAllocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    FeeRecordMetadataId = table.Column<Guid>(type: "uuid", nullable: false),
                    DueFeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<double>(type: "numeric(18,2)", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeeCollectionDueFeeAllocations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FeePaymentReceipts_ReconciliationFilter",
                table: "FeePaymentReceipts",
                columns: new[] { "TenantId", "AcademicYearId", "ReceiptKind", "VerificationStatus", "ReceiptDate" });

            migrationBuilder.CreateIndex(
                name: "IX_FeeCollectionDueFeeAllocations_DueFeeId",
                table: "FeeCollectionDueFeeAllocations",
                column: "DueFeeId");

            migrationBuilder.CreateIndex(
                name: "IX_FeeCollectionDueFeeAllocations_FeeRecordMetadataId",
                table: "FeeCollectionDueFeeAllocations",
                column: "FeeRecordMetadataId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FeeCollectionDueFeeAllocations");

            migrationBuilder.DropIndex(
                name: "IX_FeePaymentReceipts_ReconciliationFilter",
                table: "FeePaymentReceipts");
        }
    }
}
