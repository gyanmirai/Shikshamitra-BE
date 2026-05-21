using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class FixReceiptNumberUniqueConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_FeeRecordMetadatas_ReceiptNumber",
                table: "FeeRecordMetadatas");

            migrationBuilder.DropIndex(
                name: "UX_FeePaymentReceipts_ReceiptNumber",
                table: "FeePaymentReceipts");

            migrationBuilder.CreateIndex(
                name: "UX_FeeRecordMetadatas_TenantId_ReceiptNumber",
                table: "FeeRecordMetadatas",
                columns: new[] { "TenantId", "ReceiptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_FeePaymentReceipts_TenantId_ReceiptNumber",
                table: "FeePaymentReceipts",
                columns: new[] { "TenantId", "ReceiptNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_FeeRecordMetadatas_TenantId_ReceiptNumber",
                table: "FeeRecordMetadatas");

            migrationBuilder.DropIndex(
                name: "UX_FeePaymentReceipts_TenantId_ReceiptNumber",
                table: "FeePaymentReceipts");

            migrationBuilder.CreateIndex(
                name: "UX_FeeRecordMetadatas_ReceiptNumber",
                table: "FeeRecordMetadatas",
                column: "ReceiptNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_FeePaymentReceipts_ReceiptNumber",
                table: "FeePaymentReceipts",
                column: "ReceiptNumber",
                unique: true);
        }
    }
}
