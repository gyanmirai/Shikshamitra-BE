using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Setting.FeeStructure;

public class FeeStructureStandard : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public Guid FeeStructureId { get; private set; }
    public Guid StandardId { get; private set; }

    public string Name { get; private set; }

    public ICollection<FeeStructureDetail> FeeStructureDetails { get; private set; }

    protected FeeStructureStandard()
    {
        FeeStructureDetails = new List<FeeStructureDetail>();
    }

    public FeeStructureStandard(
        Guid id,
        Guid? tenantId,
        Guid feeStructureId,
        Guid standardId,
        string name) : base(id)
    {
        TenantId = tenantId;
        FeeStructureId = feeStructureId;
        StandardId = standardId;
        Name = name;
        FeeStructureDetails = new List<FeeStructureDetail>();
    }
}
