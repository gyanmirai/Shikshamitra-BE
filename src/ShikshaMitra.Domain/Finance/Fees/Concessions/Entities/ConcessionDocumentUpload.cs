using System;
using ShikshaMitra.Finance.Fees.Concessions;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Fees.ConcessionRequests;

/// <summary>
/// Binds a staged concession document to the tenant, uploader and configured
/// document requirement that accepted it. A staged upload can only be attached
/// to one concession request.
/// </summary>
public class ConcessionDocumentUpload : CreationAuditedEntity<Guid>, IMultiTenant
{
	public const int StagingLifetimeHours = 24;

	public Guid? TenantId { get; private set; }
	public Guid ConcessionTypeId { get; private set; }
	public Guid DocumentId { get; private set; }
	public Guid UploadedBy { get; private set; }
	public string Url { get; private set; } = string.Empty;
	public DateTime ExpiresOn { get; private set; }
	public Guid? ConcessionRequestId { get; private set; }
	public DateTime? AttachedOn { get; private set; }

	private ConcessionDocumentUpload() { }

	public ConcessionDocumentUpload(
		Guid id,
		Guid? tenantId,
		Guid concessionTypeId,
		Guid documentId,
		Guid uploadedBy,
		string url,
		DateTime expiresOn)
	{
		if (id == Guid.Empty || concessionTypeId == Guid.Empty || documentId == Guid.Empty ||
			uploadedBy == Guid.Empty || string.IsNullOrWhiteSpace(url) || url.Trim().Length > 2048 ||
			expiresOn == default)
			throw new BusinessException(FinanceConcessionErrorCodes.DocumentOwnershipMismatch);

		Id = id;
		TenantId = tenantId;
		ConcessionTypeId = concessionTypeId;
		DocumentId = documentId;
		UploadedBy = uploadedBy;
		Url = url.Trim();
		ExpiresOn = expiresOn;
	}

	public void Attach(
		Guid concessionRequestId,
		Guid actorId,
		Guid concessionTypeId,
		Guid documentId,
		DateTime attachedOn)
	{
		if (concessionRequestId == Guid.Empty || actorId != UploadedBy ||
			concessionTypeId != ConcessionTypeId || documentId != DocumentId ||
			(ConcessionRequestId.HasValue && ConcessionRequestId != concessionRequestId))
			throw new BusinessException(FinanceConcessionErrorCodes.DocumentOwnershipMismatch);
		if (attachedOn >= ExpiresOn)
			throw new BusinessException(FinanceConcessionErrorCodes.StagedDocumentExpired);

		ConcessionRequestId = concessionRequestId;
		AttachedOn = attachedOn;
	}

	public void ReleaseFromDraft(Guid concessionRequestId, DateTime expiresOn)
	{
		if (ConcessionRequestId != concessionRequestId || concessionRequestId == Guid.Empty)
			throw new BusinessException(FinanceConcessionErrorCodes.DocumentOwnershipMismatch);

		ConcessionRequestId = null;
		AttachedOn = null;
		ExpiresOn = expiresOn;
	}
}
