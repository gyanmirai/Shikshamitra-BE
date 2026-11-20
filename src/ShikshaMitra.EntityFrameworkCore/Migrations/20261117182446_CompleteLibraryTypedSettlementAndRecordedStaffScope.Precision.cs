using Microsoft.EntityFrameworkCore.Migrations;

namespace ShikshaMitra.Migrations;

public partial class CompleteLibraryTypedSettlementAndRecordedStaffScope
{
	private static readonly (string Table, string Column)[] SettlementMoneyColumns =
	[
		("Finance_FeeRecordMetadatas", "TotalPaidFee"), ("Finance_ChargeCollectionRecords", "Amount")
	];

	private static void PreflightLegacySettlementMoney(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql("""
			LOCK TABLE "Finance_FeeRecordMetadatas", "Finance_ChargeCollectionRecords" IN SHARE ROW EXCLUSIVE MODE;
			SET LOCAL extra_float_digits=3;
			""");
		foreach (var (table, column) in SettlementMoneyColumns)
			migrationBuilder.Sql($$"""
				DO $library_money_review$
				BEGIN
				    IF EXISTS (SELECT 1 FROM "{{table}}" WHERE CASE
				        WHEN "{{column}}"::text IN ('NaN','Infinity','-Infinity') THEN true
				        WHEN ("{{column}}"::text)::numeric < 0 OR ("{{column}}"::text)::numeric >= 10000000000000000 THEN true
				        WHEN ("{{column}}"::text)::numeric <> trunc(("{{column}}"::text)::numeric, 2) THEN true
				        ELSE (("{{column}}"::text)::numeric(18,2))::double precision IS DISTINCT FROM "{{column}}" END) THEN
				        RAISE EXCEPTION USING ERRCODE='P0001', MESSAGE='Library money precision review is required for {{table}}.{{column}}. The original values have not been rounded or replaced.';
				    END IF;
				END;
				$library_money_review$;
				""");
	}

	private static void ConvertLegacySettlementMoney(MigrationBuilder migrationBuilder, string table, string column) =>
		migrationBuilder.Sql($$"""
			ALTER TABLE "{{table}}" ALTER COLUMN "{{column}}" TYPE numeric(18,2) USING ("{{column}}"::text)::numeric(18,2);
			""");

	private static void PreflightSettlementMoneyRollback(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql("SET LOCAL extra_float_digits=3;");
		foreach (var (table, column) in SettlementMoneyColumns)
			migrationBuilder.Sql($$"""
				DO $library_money_rollback$
				BEGIN
				    IF EXISTS (SELECT 1 FROM "{{table}}" WHERE CASE
				        WHEN "{{column}}"::text IN ('NaN','Infinity','-Infinity') THEN true
				        ELSE "{{column}}" < 0 OR "{{column}}" IS DISTINCT FROM (("{{column}}"::double precision)::text)::numeric END) THEN
				        RAISE EXCEPTION 'Library settlement rollback cannot preserve the exact money in {{table}}.{{column}}. Retain the decimal schema.';
				    END IF;
				END;
				$library_money_rollback$;
				""");
	}
}
