using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Setting.FeeStructure;

public class FeeStructureDetail : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public Guid FeeStructureStandardId { get; private set; }
    public Guid FeeHeadId { get; private set; }

    public decimal Amount { get; private set; }
    public bool IsOneTime { get; private set; }
    public bool IsOptional { get; private set; }

    protected FeeStructureDetail() { }

    public FeeStructureDetail(
        Guid id,
        Guid? tenantId,
        Guid feeStructureStandardId,
        Guid feeHeadId,
        decimal amount,
        bool isOneTime,
        bool isOptional) : base(id)
    {
        TenantId = tenantId;
        FeeStructureStandardId = feeStructureStandardId;
        FeeHeadId = feeHeadId;
        Amount = amount;
        IsOneTime = isOneTime;
        IsOptional = isOptional;
    }
}
