using System;
using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.Finance.ExternalReceivables;

internal static class FinanceExternalAcceptedEvidenceSql
{
	public static string Build(bool postgres)
	{
		var fields = new Dictionary<string, string>();
		var collation = postgres ? "\"C\"" : "BINARY";
		var functions = postgres ? "public." : "";
		var document = "l.\"SourceSnapshotJson\"";
		string Exact(string value) => $"({value}) COLLATE {collation}";
		string Scalar(string path, string kind = "string") => $"{functions}\"AppFinanceTryJsonScalar\"({document}, '{path}', '{kind}')";
		string Key(string path) => Exact($"lower({Scalar(path)})");
		string NativeKey(string value) => Exact($"lower(CAST({value} AS TEXT))");
		string Trim(string value) => $"{(postgres ? "btrim" : "trim")}({value}, {{1}})";
		string Date(string value) => postgres ? $"to_char({value}, 'YYYY-MM-DD')" : $"strftime('%Y-%m-%d', {value})";
		void Add(string name, string value) => fields.Add(name, value);

		Add("LinkId", "l.\"Id\""); Add("TenantId", "l.\"TenantId\""); Add("SourceKind", "l.\"SourceKind\"");
		Add("SourceId", "l.\"SourceId\""); Add("SourceVersion", Exact("l.\"SourceVersion\""));
		Add("StoredFingerprint", Exact("l.\"SourceFingerprint\""));
		Add("CalculatedFingerprint", Exact($"{functions}\"AppFinanceTrySourceFingerprintV1\"({document})"));
		Add("AcademicYearId", "l.\"AcademicYearId\""); Add("Currency", Exact("l.\"Currency\""));
		Add("FinanceChargeId", "l.\"FinanceChargeId\""); Add("RequestId", "l.\"FinanceChargeRequestId\"");
		Add("ActualChargeId", "c.\"Id\""); Add("ActualRequestId", "c.\"ChargeRequestId\"");
		Add("ActualYearId", "c.\"AcademicYearId\""); Add("StudentId", "c.\"StudentId\""); Add("EmployeeId", "c.\"EmployeeId\"");
		Add("Amount", "c.\"Amount\""); Add("Status", "c.\"Status\"");
		Add("SchoolAssessmentDate", "c.\"SchoolAssessmentDate\""); Add("SchoolDueDate", "c.\"SchoolDueDate\"");
		Add("TenantKey", NativeKey("l.\"TenantId\"")); Add("SourceKey", NativeKey("l.\"SourceId\""));
		Add("YearKey", NativeKey("l.\"AcademicYearId\"")); Add("StudentKey", NativeKey("c.\"StudentId\""));
		Add("EmployeeKey", NativeKey("c.\"EmployeeId\""));
		Add("AssessmentDateText", Date("c.\"SchoolAssessmentDate\"")); Add("DueDateText", Date("c.\"SchoolDueDate\""));
		Add("SnapshotTenant", Key("TenantId")); Add("SnapshotKind", Scalar("Source.Kind", "number"));
		Add("SnapshotSource", Key("Source.SourceId")); Add("SnapshotVersion", Exact(Scalar("Source.SourceVersion")));
		Add("SnapshotYear", Key("AcademicYearId")); Add("SnapshotYearConfirmed", Scalar("AcademicYearConfirmed", "boolean"));
		Add("SnapshotYearEvidence", Exact(Scalar("AcademicYearEvidenceJson")));
		Add("SnapshotYearHasEvidence", $"COALESCE({Trim(Scalar("AcademicYearEvidenceJson"))}, '') <> ''");
		Add("SnapshotPayerKind", Scalar("Payer.Kind", "number")); Add("SnapshotStudent", Key("Payer.StudentId"));
		Add("SnapshotStudentNull", $"COALESCE({Scalar("Payer.StudentId", "null")} = 'null', FALSE)");
		Add("SnapshotEmployee", Key("Payer.EmployeeId"));
		Add("SnapshotEmployeeNull", $"COALESCE({Scalar("Payer.EmployeeId", "null")} = 'null', FALSE)");
		Add("SnapshotAmount", $"{functions}\"AppFinanceTryJsonMoney\"({document}, 'Amount')");
		Add("SnapshotCurrency", Exact(Scalar("Currency"))); Add("SnapshotAssessmentDate", Exact(Scalar("AssessmentDate")));
		Add("SnapshotDueDate", Exact(Scalar("DueDate"))); Add("SnapshotDatesConfirmed", Scalar("SchoolDatesConfirmed", "boolean"));
		Add("SnapshotReason", Exact(Trim(Scalar("Reason")))); Add("SnapshotDateEvidence", Exact(Scalar("EvidenceReference")));
		Add("SnapshotDateHasEvidence", $"COALESCE({Trim(Scalar("EvidenceReference"))}, '') <> ''");
		// The native kind probe runs only after safe scalar extraction proved that
		// this column is a valid JSON document. Malformed siblings cannot throw.
		var originKind = postgres ? $"json_typeof(({document})::json -> 'ReasonOrigin')" : $"json_type({document}, '$.ReasonOrigin')";
		Add("OriginKind", $"CASE WHEN {Scalar("Source.SourceId")} IS NOT NULL THEN {originKind} ELSE 'invalid' END");
		Add("OriginSchema", Scalar("ReasonOrigin.Schema", "number")); Add("OriginCategory", Key("ReasonOrigin.CategoryId"));
		var remarks = Scalar("ReasonOrigin.RawRemarks");
		Add("OriginRemarks", Exact(remarks));
		Add("OriginRemarksNull", $"COALESCE({Scalar("ReasonOrigin.RawRemarks", "null")} = 'null', FALSE)");
		Add("OriginRemarksHasText", $"CASE WHEN {remarks} IS NOT NULL THEN {Trim(remarks)} <> '' WHEN {Scalar("ReasonOrigin.RawRemarks", "null")} = 'null' THEN FALSE ELSE NULL END");
		Add("OriginUsesCategory", Scalar("ReasonOrigin.UsesCategoryReason", "boolean"));

		var schema = ShikshaMitraConsts.DbSchema;
		var prefix = string.IsNullOrEmpty(schema) ? "" : $"\"{schema.Replace("\"", "\"\"")}\".";
		return $"SELECT {string.Join(",\n", fields.Select(field => $"{field.Value} AS \"{field.Key}\""))}\n" +
			$"FROM {prefix}\"Finance_ExternalReceivableSourceLinks\" AS l\n" +
			$"LEFT JOIN {prefix}\"Finance_Charges\" AS c ON c.\"Id\" = l.\"FinanceChargeId\" AND c.\"TenantId\" = {{0}} AND NOT c.\"IsDeleted\"\n" +
			"WHERE l.\"TenantId\" = {0}";
	}
}
