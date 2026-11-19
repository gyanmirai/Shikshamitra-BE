using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class LinkConcessionRefundRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ConcessionRequestId",
                table: "Finance_FeeRefundRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_FeeRefundRequests_Concession_Receipt",
                table: "Finance_FeeRefundRequests",
                columns: new[] { "ConcessionRequestId", "OriginalReceiptId" },
                unique: true,
                filter: "\"ConcessionRequestId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_FeeRefundRequests_Concession_Receipt",
                table: "Finance_FeeRefundRequests");

            migrationBuilder.DropColumn(
                name: "ConcessionRequestId",
                table: "Finance_FeeRefundRequests");
        }
    }
}
