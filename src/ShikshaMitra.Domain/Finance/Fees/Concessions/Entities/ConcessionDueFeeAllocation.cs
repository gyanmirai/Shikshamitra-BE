using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Fees.ConcessionRequests;

public class ConcessionDueFeeAllocation : FullAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid ConcessionRequestId { get; private set; }
	public Guid DueFeeId { get; private set; }
	public Guid FeeHeadId { get; private set; }
	public decimal ConcessionAmount { get; private set; }

	private ConcessionDueFeeAllocation() { }

	public ConcessionDueFeeAllocation(
		Guid id,
		Guid? tenantId,
		Guid concessionRequestId,
		Guid dueFeeId,
		Guid feeHeadId,
		decimal concessionAmount)
	{
		Id = id;
		TenantId = tenantId;
		ConcessionRequestId = concessionRequestId;
		DueFeeId = dueFeeId;
		FeeHeadId = feeHeadId;
		ConcessionAmount = concessionAmount;
	}
}
