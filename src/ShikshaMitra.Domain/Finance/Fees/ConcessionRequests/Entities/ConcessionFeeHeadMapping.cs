using System;
using ShikshaMitra.Finance.Fees.Concessions;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Fees.ConcessionRequests;

public class ConcessionFeeHeadMapping : FullAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid ConcessionRequestId { get; private set; }
	public Guid FeeHeadId { get; private set; }
	public decimal PreConcessionFeeHeadAmount { get; private set; }
	public decimal FeeHeadConcessionAmount { get; private set; }
	public decimal PostConcessionFeeHeadAmount { get; private set; }
	public DiscountBasis UserSpecifiedConcessionAmountType { get; private set; }
	public decimal UserSpecifiedConcessionAmountValue { get; private set; }

	private ConcessionFeeHeadMapping() { }

	public ConcessionFeeHeadMapping(
		Guid id,
		Guid? tenantId,
		Guid concessionRequestId,
		Guid feeHeadId,
		decimal preAmount,
		decimal discountAmount,
		decimal postAmount,
		DiscountBasis basis,
		decimal userValue)
	{
		Id = id;
		TenantId = tenantId;
		ConcessionRequestId = concessionRequestId;
		FeeHeadId = feeHeadId;
		PreConcessionFeeHeadAmount = preAmount;
		FeeHeadConcessionAmount = discountAmount;
		PostConcessionFeeHeadAmount = postAmount;
		UserSpecifiedConcessionAmountType = basis;
		UserSpecifiedConcessionAmountValue = userValue;
	}
}
