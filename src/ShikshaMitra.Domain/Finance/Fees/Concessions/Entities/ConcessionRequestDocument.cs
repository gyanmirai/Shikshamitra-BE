using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Fees.ConcessionRequests;

public class ConcessionRequestDocument : FullAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid ConcessionRequestId { get; private set; }
	public Guid DocumentId { get; private set; }
	public string Url { get; private set; } = string.Empty;
	public int Status { get; private set; }

	private ConcessionRequestDocument() { }

	public ConcessionRequestDocument(
		Guid id,
		Guid? tenantId,
		Guid concessionRequestId,
		Guid documentId,
		string url,
		int status)
	{
		Id = id;
		TenantId = tenantId;
		ConcessionRequestId = concessionRequestId;
		DocumentId = documentId;
		Url = url;
		Status = status;
	}

	public void UpdateSubmission(string url, int status)
	{
		Url = url;
		Status = status;
	}
}
