using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.Receiving;

public sealed class LibraryReceivedCopy : Entity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid ReceiptLineId { get; private set; }
	public Guid CopyId { get; private set; }
	public string AccessionNumber { get; private set; } = string.Empty;
	public int CopyNumber { get; private set; }
	private LibraryReceivedCopy() { }

	internal LibraryReceivedCopy(Guid tenantId, Guid lineId, LibraryReceivedCopyEvidence evidence) : base(Guid.NewGuid())
	{
		TenantId = tenantId; ReceiptLineId = lineId; CopyId = evidence.CopyId;
		AccessionNumber = evidence.AccessionNumber.Trim(); CopyNumber = evidence.CopyNumber;
	}
}
