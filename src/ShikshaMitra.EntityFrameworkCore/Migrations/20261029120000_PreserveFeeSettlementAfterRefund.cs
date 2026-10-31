using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShikshaMitra.EntityFrameworkCore;

namespace ShikshaMitra.Migrations;

[DbContext(typeof(ShikshaMitraDbContext))]
[Migration("20261029120000_PreserveFeeSettlementAfterRefund")]
public class PreserveFeeSettlementAfterRefund : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		// Repair derived collection balances only. Payments, refunds, credits and
		// their receipt/accounting evidence retain their recorded values.
		migrationBuilder.Sql("""
			UPDATE "Finance_DueFees"
			SET "OutstandingAmount" = CASE
			      WHEN "OriginalAmount" - "PaidAmount" - "NetAmount" > 0
			      THEN "OriginalAmount" - "PaidAmount" - "NetAmount" ELSE 0 END,
			    "Status" = CASE
			      WHEN "OriginalAmount" - "PaidAmount" - "NetAmount" <= 0 THEN 2
			      WHEN "PaidAmount" > 0 OR "NetAmount" > 0 THEN 1 ELSE 0 END
			WHERE "IsDeleted" = FALSE AND "RefundedAmount" > 0
			  AND ("OutstandingAmount" <> CASE
			         WHEN "OriginalAmount" - "PaidAmount" - "NetAmount" > 0
			         THEN "OriginalAmount" - "PaidAmount" - "NetAmount" ELSE 0 END
			    OR "Status" <> CASE
			         WHEN "OriginalAmount" - "PaidAmount" - "NetAmount" <= 0 THEN 2
			         WHEN "PaidAmount" > 0 OR "NetAmount" > 0 THEN 1 ELSE 0 END);
			""");
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		// Restore the prior derived-balance rule if the application is rolled back.
		migrationBuilder.Sql("""
			UPDATE "Finance_DueFees"
			SET "OutstandingAmount" = CASE
			      WHEN "OriginalAmount" - "PaidAmount" + "RefundedAmount" - "NetAmount" > 0
			      THEN "OriginalAmount" - "PaidAmount" + "RefundedAmount" - "NetAmount" ELSE 0 END,
			    "Status" = CASE
			      WHEN "OriginalAmount" - "PaidAmount" + "RefundedAmount" - "NetAmount" <= 0 THEN 2
			      WHEN "PaidAmount" - "RefundedAmount" > 0 OR "NetAmount" > 0 THEN 1 ELSE 0 END
			WHERE "IsDeleted" = FALSE AND "RefundedAmount" > 0;
			""");
	}
}
