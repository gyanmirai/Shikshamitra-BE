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
            migrationBuilder.CreateTable(
                name: "FeePaymentReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    FeeCollectionTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceiptNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ReceiptKind = table.Column<int>(type: "integer", nullable: false),
                    ReceiptScope = table.Column<int>(type: "integer", nullable: false),
                    VerificationStatus = table.Column<int>(type: "integer", nullable: false),
                    TotalPaidAmount = table.Column<double>(type: "numeric(18,2)", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    BoardId = table.Column<Guid>(type: "uuid", nullable: false),
                    EducationMediumId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceiptDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PdfBlobName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LinkedProvisionalReceiptId = table.Column<Guid>(type: "uuid", nullable: true),
                    LinkedFinalReceiptId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReceiptBookId = table.Column<Guid>(type: "uuid", nullable: true),
                    Remarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_FeePaymentReceipts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FeePaymentReceipts_FeeCollectionTransactionId",
                table: "FeePaymentReceipts",
                column: "FeeCollectionTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_FeePaymentReceipts_ListFilter",
                table: "FeePaymentReceipts",
                columns: new[] { "TenantId", "AcademicYearId", "BoardId", "EducationMediumId", "ReceiptKind" });

            migrationBuilder.CreateIndex(
                name: "UX_FeePaymentReceipts_ReceiptNumber",
                table: "FeePaymentReceipts",
                column: "ReceiptNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FeePaymentReceipts");
        }
    }
}
