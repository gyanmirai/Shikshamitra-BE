using System;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Library.Books;
using Volo.Abp;

namespace ShikshaMitra.Library.Charges;

public partial class LibraryFinanceSourceReader
{
	private async Task<(string Reason, FinanceExternalSourceReasonOrigin? Origin)> ReadReasonAsync(LibraryCharge charge)
	{
		var usesCategory = string.IsNullOrWhiteSpace(charge.Remarks);
		var category = usesCategory ? await categories.FindAsync(charge.ChargeCategoryId, includeDetails: false) : null;
		var currentReason = usesCategory ? category?.ChargeType ?? string.Empty : charge.Remarks!;
		var currentOrigin = new FinanceExternalSourceReasonOrigin(1, charge.ChargeCategoryId, charge.Remarks, usesCategory);
		if (!charge.FinanceSourceLinkId.HasValue) return (currentReason, currentOrigin);
		var link = await links.FindAsync(charge.FinanceSourceLinkId.Value, includeDetails: false);
		if (link == null || link.TenantId != tenant.Id || link.SourceKind != FinanceExternalSourceKind.LibraryAssessment ||
			link.SourceId != charge.Id || link.SourceVersion != charge.GetAssessmentVersion())
			throw new BusinessException(LibraryFinanceErrorCodes.InvalidSource);
		FinanceExternalReceivableSourceFacts? accepted;
		try { accepted = JsonSerializer.Deserialize<FinanceExternalReceivableSourceFacts>(link.SourceSnapshotJson); }
		catch (JsonException)
		{
			throw new BusinessException(LibraryFinanceErrorCodes.SourceRequiresReview, "The original assessment reason evidence requires review.");
		}
		if (accepted == null) throw ReasonChanged();
		// Existing snapshots without captured origin retain the strict current-facts comparison.
		if (accepted.ReasonOrigin == null) return (currentReason, null);
		var origin = accepted.ReasonOrigin;
		if (accepted.Source == null || accepted.Payer == null || string.IsNullOrWhiteSpace(accepted.Reason) ||
			accepted.TenantId != tenant.Id || accepted.Source.Kind != link.SourceKind || accepted.Source.SourceId != link.SourceId ||
			accepted.Source.SourceVersion != link.SourceVersion || FinanceExternalReceivableFingerprint.Source(accepted) != link.SourceFingerprint ||
			origin != currentOrigin || !OriginHasExactTypes(link.SourceSnapshotJson, origin))
			throw ReasonChanged();
		return (accepted.Reason, origin);
	}

	private static bool OriginHasExactTypes(string json, FinanceExternalSourceReasonOrigin origin) =>
		FinanceExternalEvidenceJson.Scalar(json, "ReasonOrigin.Schema", "number") == "1" &&
		FinanceExternalEvidenceJson.Scalar(json, "ReasonOrigin.CategoryId", "string") == origin.CategoryId.ToString("D") &&
		FinanceExternalEvidenceJson.Scalar(json, "ReasonOrigin.UsesCategoryReason", "boolean") == (origin.UsesCategoryReason ? "true" : "false") &&
		(origin.RawRemarks == null
			? FinanceExternalEvidenceJson.Scalar(json, "ReasonOrigin.RawRemarks", "null") == "null"
			: FinanceExternalEvidenceJson.Scalar(json, "ReasonOrigin.RawRemarks", "string") == origin.RawRemarks);

	private static BusinessException ReasonChanged() => new(LibraryFinanceErrorCodes.SourceChanged,
		"The assessment reason evidence changed. Review the original source before continuing.");
}
