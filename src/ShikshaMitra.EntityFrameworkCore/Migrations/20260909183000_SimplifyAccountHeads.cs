using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations;

public partial class SimplifyAccountHeads : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		// Retain legacy configuration as archival metadata for rollback; IDs and all links stay intact.
		migrationBuilder.Sql("""
			UPDATE "Finance_AccountHeads"
			SET "ExtraProperties" = (COALESCE(NULLIF("ExtraProperties", ''), '{}')::jsonb ||
				jsonb_build_object('LegacyAccountHeadSettings20260909', jsonb_build_object(
					'Code', "Code", 'NormalizedCode', "NormalizedCode", 'CurrencyCode', "CurrencyCode",
					'PostingAllowed', "PostingAllowed", 'IsControlAccount', "IsControlAccount",
					'EffectiveFrom', "EffectiveFrom", 'EffectiveTo', "EffectiveTo")))::text;
			""");
		migrationBuilder.DropIndex("IX_Finance_AccountHeads_TenantId_AcademicYearId_NormalizedCode", "Finance_AccountHeads");
		foreach (var column in new[] { "Code", "NormalizedCode", "CurrencyCode", "PostingAllowed", "IsControlAccount", "EffectiveFrom", "EffectiveTo" })
			migrationBuilder.DropColumn(column, "Finance_AccountHeads");
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.AddColumn<string>("Code", "Finance_AccountHeads", type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "");
		migrationBuilder.AddColumn<string>("NormalizedCode", "Finance_AccountHeads", type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "");
		migrationBuilder.AddColumn<string>("CurrencyCode", "Finance_AccountHeads", type: "character varying(3)", maxLength: 3, nullable: false, defaultValue: "INR");
		migrationBuilder.AddColumn<bool>("PostingAllowed", "Finance_AccountHeads", type: "boolean", nullable: false, defaultValue: true);
		migrationBuilder.AddColumn<bool>("IsControlAccount", "Finance_AccountHeads", type: "boolean", nullable: false, defaultValue: false);
		migrationBuilder.AddColumn<DateTimeOffset>("EffectiveFrom", "Finance_AccountHeads", type: "timestamp with time zone", nullable: false, defaultValue: DateTimeOffset.UnixEpoch);
		migrationBuilder.AddColumn<DateTimeOffset>("EffectiveTo", "Finance_AccountHeads", type: "timestamp with time zone", nullable: true);
		migrationBuilder.Sql("""
			UPDATE "Finance_AccountHeads"
			SET "Code" = COALESCE("ExtraProperties"::jsonb #>> '{LegacyAccountHeadSettings20260909,Code}', upper(replace("Id"::text, '-', ''))),
				"NormalizedCode" = COALESCE("ExtraProperties"::jsonb #>> '{LegacyAccountHeadSettings20260909,NormalizedCode}', upper(replace("Id"::text, '-', ''))),
				"CurrencyCode" = COALESCE("ExtraProperties"::jsonb #>> '{LegacyAccountHeadSettings20260909,CurrencyCode}', 'INR'),
				"PostingAllowed" = COALESCE(("ExtraProperties"::jsonb #>> '{LegacyAccountHeadSettings20260909,PostingAllowed}')::boolean, true),
				"IsControlAccount" = COALESCE(("ExtraProperties"::jsonb #>> '{LegacyAccountHeadSettings20260909,IsControlAccount}')::boolean, false),
				"EffectiveFrom" = COALESCE(("ExtraProperties"::jsonb #>> '{LegacyAccountHeadSettings20260909,EffectiveFrom}')::timestamptz, '1970-01-01T00:00:00Z'::timestamptz),
				"EffectiveTo" = ("ExtraProperties"::jsonb #>> '{LegacyAccountHeadSettings20260909,EffectiveTo}')::timestamptz,
				"ExtraProperties" = ("ExtraProperties"::jsonb - 'LegacyAccountHeadSettings20260909')::text;
			""");
		migrationBuilder.CreateIndex("IX_Finance_AccountHeads_TenantId_AcademicYearId_NormalizedCode", "Finance_AccountHeads", new[] { "TenantId", "AcademicYearId", "NormalizedCode" }, unique: true);
	}
}
