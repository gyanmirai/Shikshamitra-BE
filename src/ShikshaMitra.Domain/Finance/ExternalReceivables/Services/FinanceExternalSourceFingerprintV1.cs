using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace ShikshaMitra.Finance.ExternalReceivables;

// Version one preserves the existing accepted byte protocol; it is not a JSON hash.
public static class FinanceExternalSourceFingerprintV1
{
	private static readonly JsonSerializerOptions ProtocolOptions = new()
	{
		Encoder = JavaScriptEncoder.Create(UnicodeRanges.BasicLatin)
	};

	public static string Source(FinanceExternalFingerprintFacts facts) => Convert.ToHexString(SHA256.HashData(
		Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
		{
			Schema = 1,
			facts.TenantId,
			facts.Kind,
			facts.SourceId,
			facts.SourceVersion,
			facts.AcademicYearId,
			facts.AcademicYearConfirmed,
			facts.AcademicYearEvidenceJson,
			facts.PayerKind,
			facts.StudentId,
			facts.EmployeeId,
			Amount = facts.Amount.ToString("G29", CultureInfo.InvariantCulture),
			facts.Currency,
			AssessmentDate = facts.AssessmentDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
			DueDate = facts.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
			facts.SchoolDatesConfirmed,
			Reason = facts.Reason.Trim(),
			facts.EvidenceReference
		}, ProtocolOptions)))).ToLowerInvariant();

	public static string? TryFromSnapshot(string? snapshot)
	{
		string? Read(string path, string kind = "string") => FinanceExternalEvidenceJson.Scalar(snapshot, path, kind);
		var version = Read("Source.SourceVersion");
		var kind = Read("Source.Kind", "number");
		var payer = Read("Payer.Kind", "number");
		var yearConfirmed = Flag(Read("AcademicYearConfirmed", "boolean"));
		var datesConfirmed = Flag(Read("SchoolDatesConfirmed", "boolean"));
		var yearEvidence = Read("AcademicYearEvidenceJson");
		var currency = Read("Currency");
		var reason = Read("Reason");
		var evidence = Read("EvidenceReference");
		var amount = FinanceExternalEvidenceJson.Money(snapshot, "Amount");
		if (version == null || kind != "1" || payer is not ("1" or "2") ||
			!yearConfirmed.HasValue || !datesConfirmed.HasValue || yearEvidence == null ||
			currency == null || reason == null || evidence == null || !amount.HasValue ||
			!Key(Read("TenantId"), out var tenant) || !Key(Read("Source.SourceId"), out var source) ||
			!Key(Read("AcademicYearId"), out var year) || !NullableKey(snapshot, "Payer.StudentId", out var student) ||
			!NullableKey(snapshot, "Payer.EmployeeId", out var employee) ||
			!Date(Read("AssessmentDate"), out var assessmentDate) || !Date(Read("DueDate"), out var dueDate))
			return null;
		return Source(new(tenant, FinanceExternalSourceKind.LibraryAssessment, source, version, year,
			yearConfirmed.Value, yearEvidence, payer == "1" ? FinanceExternalPayerKind.Student : FinanceExternalPayerKind.Employee,
			student, employee, amount.Value, currency, assessmentDate, dueDate, datesConfirmed.Value, reason, evidence));
	}

	private static bool? Flag(string? value) => value switch { "true" => true, "false" => false, _ => null };
	private static bool Key(string? value, out Guid key)
	{
		key = default;
		return value?.Length == 36 && Guid.TryParseExact(value, "D", out key) &&
			string.Equals(value, key.ToString("D"), StringComparison.OrdinalIgnoreCase);
	}
	private static bool NullableKey(string? snapshot, string path, out Guid? key)
	{
		key = null;
		if (Key(FinanceExternalEvidenceJson.Scalar(snapshot, path, "string"), out var parsed))
		{ key = parsed; return true; }
		return FinanceExternalEvidenceJson.Scalar(snapshot, path, "null") == "null";
	}
	private static bool Date(string? value, out DateOnly date) => DateOnly.TryParseExact(value, "yyyy-MM-dd",
		CultureInfo.InvariantCulture, DateTimeStyles.None, out date) && value == date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
