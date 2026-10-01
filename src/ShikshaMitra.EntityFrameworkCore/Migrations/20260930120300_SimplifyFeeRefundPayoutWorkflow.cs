using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShikshaMitra.EntityFrameworkCore;

#nullable disable

namespace ShikshaMitra.Migrations;

[DbContext(typeof(ShikshaMitraDbContext))]
[Migration("20260930120300_SimplifyFeeRefundPayoutWorkflow")]
public class SimplifyFeeRefundPayoutWorkflow : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Finance_FeeRefundAccountingEntries",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                RefundRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                SettlementId = table.Column<Guid>(type: "uuid", nullable: false),
                OriginalReceiptId = table.Column<Guid>(type: "uuid", nullable: true),
                BankAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                Method = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                ExternalReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                IsCorrection = table.Column<bool>(type: "boolean", nullable: false),
                CorrectsEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                FeeHeadAmountsJson = table.Column<string>(type: "jsonb", nullable: false),
                EvidenceReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                PostedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                PostedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Finance_FeeRefundAccountingEntries", x => x.Id));
        migrationBuilder.CreateIndex("IX_FeeRefundAccountingEntries_Tenant_Request_Correction",
            "Finance_FeeRefundAccountingEntries", new[] { "TenantId", "RefundRequestId", "IsCorrection" }, unique: true);
        migrationBuilder.CreateIndex("IX_FeeRefundAccountingEntries_SettlementId",
            "Finance_FeeRefundAccountingEntries", "SettlementId");
        migrationBuilder.CreateIndex("IX_FeeRefundAccountingEntries_OriginalReceiptId",
            "Finance_FeeRefundAccountingEntries", "OriginalReceiptId");
        migrationBuilder.AddColumn<Guid>("ReplacesRefundRequestId", "Finance_FeeRefundRequests", nullable: true);
        migrationBuilder.AddColumn<string>("RequestIdempotencyKey", "Finance_FeeRefundRequests", type: "character varying(100)", maxLength: 100, nullable: true);
        migrationBuilder.AddColumn<int>("WorkflowVersion", "Finance_FeeRefundRequests", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<Guid>("BankAccountId", "Finance_FeeRefundSettlements", nullable: true);
        migrationBuilder.AddColumn<string>("EvidenceReference", "Finance_FeeRefundSettlements", type: "character varying(500)", maxLength: 500, nullable: true);
        migrationBuilder.AddColumn<Guid>("VerifiedByUserId", "Finance_FeeRefundSettlements", nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>("VerifiedAt", "Finance_FeeRefundSettlements", nullable: true);
        migrationBuilder.AddColumn<string>("VerificationKey", "Finance_FeeRefundSettlements", type: "character varying(100)", maxLength: 100, nullable: true);
        migrationBuilder.CreateIndex("IX_Finance_FeeRefundRequests_TenantId_RequestIdempotencyKey", "Finance_FeeRefundRequests", new[] { "TenantId", "RequestIdempotencyKey" }, unique: true, filter: "\"RequestIdempotencyKey\" IS NOT NULL");
        migrationBuilder.CreateIndex("IX_Finance_FeeRefundRequests_ReplacesRefundRequestId", "Finance_FeeRefundRequests", "ReplacesRefundRequestId", unique: true, filter: "\"ReplacesRefundRequestId\" IS NOT NULL");
        migrationBuilder.DropIndex("UX_FeeRefundRequests_Concession_Receipt", "Finance_FeeRefundRequests");
        migrationBuilder.CreateIndex("UX_FeeRefundRequests_Concession_Receipt", "Finance_FeeRefundRequests", new[] { "ConcessionRequestId", "OriginalReceiptId" }, unique: true, filter: "\"ConcessionRequestId\" IS NOT NULL AND \"Status\" NOT IN (3, 8, 9)");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("Finance_FeeRefundAccountingEntries");
        migrationBuilder.DropIndex("UX_FeeRefundRequests_Concession_Receipt", "Finance_FeeRefundRequests");
        migrationBuilder.CreateIndex("UX_FeeRefundRequests_Concession_Receipt", "Finance_FeeRefundRequests", new[] { "ConcessionRequestId", "OriginalReceiptId" }, unique: true, filter: "\"ConcessionRequestId\" IS NOT NULL");
        migrationBuilder.DropIndex("IX_Finance_FeeRefundRequests_TenantId_RequestIdempotencyKey", "Finance_FeeRefundRequests");
        migrationBuilder.DropIndex("IX_Finance_FeeRefundRequests_ReplacesRefundRequestId", "Finance_FeeRefundRequests");
        migrationBuilder.DropColumn("ReplacesRefundRequestId", "Finance_FeeRefundRequests");
        migrationBuilder.DropColumn("RequestIdempotencyKey", "Finance_FeeRefundRequests");
        migrationBuilder.DropColumn("WorkflowVersion", "Finance_FeeRefundRequests");
        migrationBuilder.DropColumn("BankAccountId", "Finance_FeeRefundSettlements");
        migrationBuilder.DropColumn("EvidenceReference", "Finance_FeeRefundSettlements");
        migrationBuilder.DropColumn("VerifiedByUserId", "Finance_FeeRefundSettlements");
        migrationBuilder.DropColumn("VerifiedAt", "Finance_FeeRefundSettlements");
        migrationBuilder.DropColumn("VerificationKey", "Finance_FeeRefundSettlements");
    }
}
