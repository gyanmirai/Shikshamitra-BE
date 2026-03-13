using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.ConcessionRequests;

/// <summary>
/// Step 5 — Document Upload. Stores an uploaded document URL per concession type per request.
/// </summary>
public class ConcessionRequestDocument : AuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }

	public Guid ConcessionRequestId { get; private set; }

	/// <summary>The concession type this document belongs to (from Step 3 mapping).</summary>
	public Guid ConcessionTypeId { get; private set; }

	public Guid ConcessionRequestConcessionTypeId { get; private set; }

	/// <summary>The document type identifier (from ConcessionTypeDocument definitions).</summary>
	public Guid DocumentId { get; private set; }

	/// <summary>Presigned S3 or permanent URL of the uploaded file.</summary>
	public string Url { get; private set; }

	/// <summary>0 = Pending, 1 = Uploaded.</summary>
	public int Status { get; private set; }

	public DateTime? UploadedAt { get; private set; }

	protected ConcessionRequestDocument() { }

	public ConcessionRequestDocument(
		Guid id,
		Guid? tenantId,
		Guid concessionRequestId,
		Guid concessionTypeId,
		Guid concessionRequestConcessionTypeId,
		Guid documentId,
		string url,
		int status) : base(id)
	{
		TenantId = tenantId;
		ConcessionRequestId = concessionRequestId;
		ConcessionTypeId = concessionTypeId;
		ConcessionRequestConcessionTypeId = concessionRequestConcessionTypeId;
		DocumentId = documentId;
		Url = url;
		Status = status;
		UploadedAt = status == 1 ? DateTime.UtcNow : null;
	}

	public void Update(string url, int status)
	{
		Url = url;
		Status = status;
		UploadedAt = status == 1 ? DateTime.UtcNow : null;
	}
}
