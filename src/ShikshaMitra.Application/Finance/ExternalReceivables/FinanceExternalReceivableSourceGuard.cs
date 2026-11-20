using System;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Library;
using Volo.Abp;
using Volo.Abp.Authorization;

namespace ShikshaMitra.Finance.ExternalReceivables;

internal static class FinanceExternalReceivableSourceGuard
{
	public static void ValidateReference(FinanceExternalSourceReference? source)
	{
		if (source == null || source.Kind != FinanceExternalSourceKind.LibraryAssessment ||
			source.SourceId == Guid.Empty || string.IsNullOrWhiteSpace(source.SourceVersion) || source.SourceVersion.Length > 64)
			throw Error(LibraryFinanceErrorCodes.InvalidSource, "A supported assessment and its current source version are required.");
	}

	public static void ValidateFacts(FinanceExternalSourceReference requested,
		FinanceExternalReceivableSourceFacts facts, Guid? tenantId)
	{
		if (!tenantId.HasValue || facts.TenantId != tenantId.Value)
			throw new AbpAuthorizationException("External receivable source access denied.");
		ValidateReference(facts.Source);
		if (facts.Source.Kind != requested.Kind || facts.Source.SourceId != requested.SourceId)
			throw Error(LibraryFinanceErrorCodes.InvalidSource, "The source reader returned a different assessment.");
		if (!string.Equals(facts.Source.SourceVersion, requested.SourceVersion, StringComparison.Ordinal))
			throw Error(LibraryFinanceErrorCodes.SourceChanged, "The assessment changed. Refresh it before posting.");
		if (!facts.AcademicYearConfirmed || string.IsNullOrWhiteSpace(facts.AcademicYearEvidenceJson))
			throw Error(LibraryFinanceErrorCodes.SourceRequiresReview,
				"Confirm the assessment's transaction year with its own source evidence.");
		if (!facts.SchoolDatesConfirmed || facts.AcademicYearId == Guid.Empty || facts.AssessmentDate == default ||
			facts.DueDate == default || facts.DueDate < facts.AssessmentDate || string.IsNullOrWhiteSpace(facts.EvidenceReference))
			throw Error(LibraryFinanceErrorCodes.SourceRequiresReview,
				"Confirm the assessment's transaction year, school dates and source evidence before posting.");
		if (facts.Amount <= 0 || facts.Amount > FinanceChargeAmountRules.MaximumAmount ||
			!FinanceChargeAmountRules.HasSupportedScale(facts.Amount))
			throw Error(LibraryFinanceErrorCodes.InvalidAmount, "The assessment must have a supported positive amount in whole paise.");
		if (!string.Equals(facts.Currency, "INR", StringComparison.Ordinal))
			throw Error(LibraryFinanceErrorCodes.UnsupportedCurrency, "This receivable flow supports confirmed INR assessments.");
		if (string.IsNullOrWhiteSpace(facts.Reason))
			throw Error(LibraryFinanceErrorCodes.SourceRequiresReview, "The assessment needs its recorded reason.");
		ValidatePayer(facts.Payer);
	}

	private static void ValidatePayer(FinanceExternalPayerDto payer)
	{
		var student = payer.Kind == FinanceExternalPayerKind.Student && payer.StudentId.HasValue &&
			payer.StudentId != Guid.Empty && !payer.EmployeeId.HasValue;
		var employee = payer.Kind == FinanceExternalPayerKind.Employee && payer.EmployeeId.HasValue &&
			payer.EmployeeId != Guid.Empty && !payer.StudentId.HasValue;
		if (!student && !employee)
			throw Error(LibraryFinanceErrorCodes.InvalidPayer, "The source must identify one real student or employee borrower.");
	}

	private static BusinessException Error(string code, string message) => new(code, message);
}
