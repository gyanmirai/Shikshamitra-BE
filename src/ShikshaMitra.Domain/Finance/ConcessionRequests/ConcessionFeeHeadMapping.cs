using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.ConcessionRequests;

public class ConcessionFeeHeadMapping : FullAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; set; }
	public Guid ConcessionRequestId { get; set; }
	public Guid FeeHeadId { get; set; }
	public double FeeHeadConcessionAmount { get; set; }
	public double PreConcessionFeeHeadAmount { get; set; }
	public double PostConcessionFeeHeadAmount { get; set; }
	public double UserSpecifiedConcessionAmountValue { get; set; }
	public AmountType UserSpecifiedConcessionAmountType { get; set; }


	protected ConcessionFeeHeadMapping()
	{
	}
	public ConcessionFeeHeadMapping(Guid id) : base(id)
	{
	}
}
