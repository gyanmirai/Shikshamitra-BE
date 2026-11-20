using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ShikshaMitra.Finance.ExternalReceivables;

internal static class FinanceExternalReceivableFingerprint
{
	public static string Source(FinanceExternalReceivableSourceFacts facts) => FinanceExternalSourceFingerprintV1.Source(new(
		facts.TenantId, facts.Source.Kind, facts.Source.SourceId, facts.Source.SourceVersion,
		facts.AcademicYearId, facts.AcademicYearConfirmed, facts.AcademicYearEvidenceJson, facts.Payer.Kind,
		facts.Payer.StudentId, facts.Payer.EmployeeId, facts.Amount, facts.Currency, facts.AssessmentDate,
		facts.DueDate, facts.SchoolDatesConfirmed, facts.Reason, facts.EvidenceReference));

	public static string PostCommand(PostFinanceExternalReceivableInput input, string sourceFingerprint) => Hash(new
	{
		Schema = 1,
		SourceFingerprint = sourceFingerprint,
		input.FinanceChargeCategoryId,
		input.CollectionPolicyVersionId,
		input.RefundPolicyVersionId
	});

	private static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(
		Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value)))).ToLowerInvariant();
}
