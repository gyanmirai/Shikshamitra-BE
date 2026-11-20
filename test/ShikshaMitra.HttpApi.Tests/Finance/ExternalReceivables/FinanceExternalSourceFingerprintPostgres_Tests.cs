using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using ShikshaMitra.Library;
using Shouldly;

namespace ShikshaMitra.Finance.ExternalReceivables;

public sealed class FinanceExternalSourceFingerprintPostgres_Tests
{
	private const string Migration = "20261118223401_AddFinanceExternalSourceFingerprintFunctions";
	private const string Previous = "20261118195300_AddFinanceExternalPositionEvidenceFunctions";

	[LibraryPostgresFact]
	public async Task Fingerprint_V1_Postgres_Should_Match_The_Existing_Producer_Golden_Bytes()
	{
		await using var database = await LibraryMigrationPostgresFixture.CreateAsync();
		await database.ExecuteAsync(database.Script(LibraryMigrationPostgresFixture.CataloguePrice, Migration));
		await using var context = database.CreateContext();
		using var data = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "FinanceExternalFingerprintV1Vectors.json")));
		var count = 0;
		foreach (var vector in data.RootElement.GetProperty("Vectors").EnumerateArray())
		{
			var snapshot = vector.GetProperty("Snapshot").GetString();
			var row = await context.Database.SqlQueryRaw<FingerprintResult>(
				"SELECT public.\"AppFinanceTrySourceFingerprintV1\"(@input) AS \"Value\"", TextParameter(snapshot)).SingleAsync();
			row.Value.ShouldBe(vector.GetProperty("Expected").GetString());
			count++;
		}
		count.ShouldBe(30);
		foreach (var invalid in new string?[] { null, "{bad", "null", "[]", "{}", "{\"Source\":{\"SourceId\":\"bad\"}}" })
		{
			var row = await context.Database.SqlQueryRaw<FingerprintResult>(
				"SELECT public.\"AppFinanceTrySourceFingerprintV1\"(@input) AS \"Value\"", TextParameter(invalid)).SingleAsync();
			row.Value.ShouldBeNull();
		}
	}

	[LibraryPostgresFact]
	public async Task Fingerprint_V1_Postgres_Should_Match_Pinned_Json_String_Escaping()
	{
		await using var database = await LibraryMigrationPostgresFixture.CreateAsync();
		await database.ExecuteAsync(database.Script(LibraryMigrationPostgresFixture.CataloguePrice, Migration));
		await using var context = database.CreateContext();
		var samples = Enumerable.Range(1, 127).Select(code => ((char)code).ToString())
			.Concat(FinanceExternalEvidenceJson.WhiteSpaceCharacters.Select(character => character.ToString()))
			.Concat(new string?[] { null, "", "José / ગુજરાતી / बाल साहित्य", "😀 𝄞", "quote\" plus+ <>&' ` \\" });
		foreach (var sample in samples)
		{
			var row = await context.Database.SqlQueryRaw<FingerprintResult>(
				"SELECT public.\"AppFinanceJsonQuoteV1\"(@input) AS \"Value\"", TextParameter(sample)).SingleAsync();
			row.Value.ShouldBe(JsonSerializer.Serialize(sample));
		}
	}

	[LibraryPostgresFact]
	public async Task Fingerprint_V1_Postgres_Migration_Should_Reverse_Without_Rewriting_Existing_Rows()
	{
		await using var database = await LibraryMigrationPostgresFixture.CreateAsync();
		var original = await database.ScalarAsync("""SELECT jsonb_agg(to_jsonb(row) ORDER BY "Id")::text FROM "AppBooks" row""");
		await database.ExecuteAsync(database.Script(LibraryMigrationPostgresFixture.CataloguePrice, Migration));
		(await database.ScalarAsync(InstalledFunctions)).ShouldBe("2");
		await database.ExecuteAsync(database.Script(Migration, Previous));
		(await database.ScalarAsync(InstalledFunctions)).ShouldBe("0");
		await database.ExecuteAsync(database.Script(Previous, Migration));
		(await database.ScalarAsync(InstalledFunctions)).ShouldBe("2");
		(await database.ScalarAsync("""SELECT jsonb_agg(to_jsonb(row) ORDER BY "Id")::text FROM "AppBooks" row"""))
			.ShouldBe(original);
	}

	[LibraryPostgresFact]
	public async Task Fingerprint_V1_Postgres_Should_Exclude_Invalid_Siblings_Before_Count_And_Page()
	{
		await using var database = await LibraryMigrationPostgresFixture.CreateAsync();
		await database.ExecuteAsync(database.Script(LibraryMigrationPostgresFixture.CataloguePrice, Migration));
		await using var context = database.CreateContext();
		using var data = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "FinanceExternalFingerprintV1Vectors.json")));
		var valid = data.RootElement.GetProperty("Vectors")[0].GetProperty("Snapshot").GetString()!;
		var documents = new System.Collections.Generic.List<string> { valid, "{bad" };
		foreach (var mutation in new[] { "quotedNull", "stringAmount", "subcent", "paddedGuid", "missingVersion", "stringBoolean", "invalidDate", "nul", "utf16" })
		{
			var json = JsonNode.Parse(valid)!;
			switch (mutation)
			{
				case "quotedNull": json["Payer"]!["EmployeeId"] = "null"; break;
				case "stringAmount": json["Amount"] = "100.75"; break;
				case "subcent": json["Amount"] = 0.001m; break;
				case "paddedGuid": json["TenantId"] = " " + json["TenantId"]!.GetValue<string>() + " "; break;
				case "missingVersion": json["Source"]!.AsObject().Remove("SourceVersion"); break;
				case "stringBoolean": json["SchoolDatesConfirmed"] = "true"; break;
				case "invalidDate": json["DueDate"] = "2026-02-31"; break;
				case "nul" or "utf16": json["Source"]!["SourceVersion"] = "PROTOCOL_INVALID_SENTINEL"; break;
			}
			var document = json.ToJsonString();
			if (mutation is "nul" or "utf16") document = document.Replace("\"PROTOCOL_INVALID_SENTINEL\"",
				mutation == "nul" ? "\"\\u0000\"" : "\"\\uD800\"", StringComparison.Ordinal);
			documents.Add(document);
		}
		var duplicate = valid.Replace("\"AcademicYearConfirmed\":true", "\"AcademicYearConfirmed\":true,\"AcademicYearConfirmed\":true", StringComparison.Ordinal);
		duplicate.ShouldNotBe(valid);
		documents.Add(duplicate);
		var parameter = new NpgsqlParameter("documents", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = documents.ToArray() };
		var query = context.Database.SqlQueryRaw<FingerprintProbe>("""
			SELECT (candidate."Ordinal" - 1)::integer AS "Id",
			    public."AppFinanceTrySourceFingerprintV1"(candidate."Raw") AS "Fingerprint"
			FROM unnest(@documents::text[]) WITH ORDINALITY AS candidate("Raw","Ordinal")
			""", parameter);
		(await query.CountAsync()).ShouldBe(12);
		var verified = query.Where(row => row.Fingerprint != null);
		(await verified.CountAsync()).ShouldBe(1);
		(await verified.OrderBy(row => row.Id).Take(1).SingleAsync()).Id.ShouldBe(0);
		(await verified.OrderBy(row => row.Id).Skip(1).Take(1).ToListAsync()).ShouldBeEmpty();
	}

	private static NpgsqlParameter TextParameter(string? value) => new("input", NpgsqlDbType.Text) { Value = (object?)value ?? DBNull.Value };
	private const string InstalledFunctions = """
		SELECT count(*) FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace
		WHERE n.nspname='public' AND p.proname IN ('AppFinanceJsonQuoteV1','AppFinanceTrySourceFingerprintV1')
		""";
	private sealed class FingerprintResult { public string? Value { get; set; } }
	private sealed class FingerprintProbe { public int Id { get; set; } public string? Fingerprint { get; set; } }
}
