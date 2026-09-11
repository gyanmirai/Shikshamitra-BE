using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionDocumentReview : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid EvidenceId { get; private set; }
	public Guid ApplicationId { get; private set; }
	public Guid StageInstanceId { get; private set; }
	public string Decision { get; private set; } = string.Empty;
	public string? Reason { get; private set; }
	public Guid ReviewedBy { get; private set; }
	public DateTime ReviewedAt { get; private set; }
	protected AdmissionDocumentReview() { }
	public AdmissionDocumentReview(Guid id, Guid? tenantId, AdmissionDocumentEvidence evidence,
		AdmissionDocumentPolicy policy, string decision, string? reason, Guid reviewedBy, DateTime reviewedAt) : base(id)
	{
		if (decision is not ("accepted" or "rejected") || evidence.Kind == "waiver" || reviewedBy == Guid.Empty)
			throw Invalid("Choose an accepted or rejected document decision.");
		if (policy.RequireIndependentReview && (evidence.SubmittedBy == reviewedBy || evidence.FileSubmittedBy == reviewedBy || evidence.OriginalSeenBy == reviewedBy))
			throw Invalid("A different authorized staff member must review this document.");
		if (decision == "accepted" && !AdmissionDocumentRules.HasCollectionEvidence(evidence, evidence.CollectionMode))
			throw Invalid("Collect the required file and original-document evidence before accepting it.");
		if (decision == "rejected" && string.IsNullOrWhiteSpace(reason)) throw Invalid("Explain why the document was rejected and what needs replacing.");
		TenantId = tenantId; EvidenceId = evidence.Id; ApplicationId = evidence.ApplicationId; StageInstanceId = evidence.StageInstanceId;
		Decision = decision; Reason = string.IsNullOrWhiteSpace(reason) ? null : Check.Length(reason.Trim(), nameof(reason), 1024);
		ReviewedBy = reviewedBy; ReviewedAt = reviewedAt;
	}
	private static BusinessException Invalid(string message) => new(AdmissionDocumentErrorCodes.ReviewNotAllowed, message);
}
