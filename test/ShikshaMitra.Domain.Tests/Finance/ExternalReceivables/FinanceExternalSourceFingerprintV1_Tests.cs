using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using ShikshaMitra.Finance.ExternalReceivables;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance;

public sealed class FinanceExternalSourceFingerprintV1_Tests
{
	[Theory]
	[InlineData(1)]
	[InlineData(2)]
	public void Fingerprint_V1_Should_Match_The_Unchanged_Application_Producer_Golden_Bytes(int role)
	{
		using var data = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "FinanceExternalFingerprintV1Vectors.json")));
		data.RootElement.GetProperty("Framework").GetString().ShouldStartWith(".NET 9.");
		var count = 0;
		foreach (var vector in data.RootElement.GetProperty("Vectors").EnumerateArray())
		{
			if (vector.GetProperty("Role").GetInt32() != role) continue;
			var snapshot = vector.GetProperty("Snapshot").GetString()!;
			FinanceExternalSourceFingerprintV1.TryFromSnapshot(snapshot).ShouldBe(vector.GetProperty("Expected").GetString());
			count++;
		}
		count.ShouldBe(15);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("{bad")]
	[InlineData("null")]
	[InlineData("[]")]
	[InlineData("{}")]
	[InlineData("{\"Source\":{\"Kind\":1,\"SourceId\":\"bad\",\"SourceVersion\":\"v1\"}}")]
	public void Fingerprint_V1_Should_Return_Unknown_For_Unsupported_Snapshot_Evidence(string? snapshot) =>
		FinanceExternalSourceFingerprintV1.TryFromSnapshot(snapshot).ShouldBeNull();

	[Theory]
	[InlineData("quotedNull")]
	[InlineData("stringAmount")]
	[InlineData("subcentAmount")]
	[InlineData("paddedGuid")]
	[InlineData("missingVersion")]
	[InlineData("stringBoolean")]
	[InlineData("invalidDate")]
	public void Fingerprint_V1_Should_Reject_Unsupported_Types_Inside_A_Complete_Real_Snapshot(string mutation)
	{
		using var data = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "FinanceExternalFingerprintV1Vectors.json")));
		var vector = data.RootElement.GetProperty("Vectors")[0];
		var json = JsonNode.Parse(vector.GetProperty("Snapshot").GetString()!)!;
		FinanceExternalSourceFingerprintV1.TryFromSnapshot(json.ToJsonString()).ShouldBe(vector.GetProperty("Expected").GetString());
		switch (mutation)
		{
			case "quotedNull": json["Payer"]!["EmployeeId"] = "null"; break;
			case "stringAmount": json["Amount"] = "100.75"; break;
			case "subcentAmount": json["Amount"] = 0.001m; break;
			case "paddedGuid": json["TenantId"] = " " + json["TenantId"]!.GetValue<string>() + " "; break;
			case "missingVersion": json["Source"]!.AsObject().Remove("SourceVersion"); break;
			case "stringBoolean": json["SchoolDatesConfirmed"] = "true"; break;
			case "invalidDate": json["DueDate"] = "2026-02-31"; break;
		}
		FinanceExternalSourceFingerprintV1.TryFromSnapshot(json.ToJsonString()).ShouldBeNull();
	}
}
