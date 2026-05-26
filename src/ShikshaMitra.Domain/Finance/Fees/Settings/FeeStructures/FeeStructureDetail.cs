using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Collections;
using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Fees.Settings.FeeStructures;

public class FeeStructureDetail : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public Guid FeeStructureId { get; private set; }
    public Guid FeeHeadId { get; private set; }

    public double Amount { get; private set; }
    public bool IsOneTime { get; private set; }
    public bool IsOptional { get; private set; }
	public bool IsRefundable { get; private set; }

	public MonthEnum? PayableMonth { get; private set; }
	public int? PayableYear { get; private set; }

	public FeeHead FeeHead { get; private set; }

	protected FeeStructureDetail() { }

    public FeeStructureDetail(
        Guid id,
        Guid? tenantId,
        Guid feeStructureId,
        Guid feeHeadId,
        double amount,
        bool isOneTime,
        bool isOptional,
		bool isRefundable,
		MonthEnum? payableMonth,
		int? payableYear) : base(id)
    {
        TenantId = tenantId;
        FeeStructureId = feeStructureId;
        FeeHeadId = feeHeadId;
        Amount = amount;
        IsOneTime = isOneTime;
        IsOptional = isOptional;
		IsRefundable = isRefundable;
		PayableMonth = isOneTime ? payableMonth : null;
		PayableYear = isOneTime ? payableYear : null;
    }
}
