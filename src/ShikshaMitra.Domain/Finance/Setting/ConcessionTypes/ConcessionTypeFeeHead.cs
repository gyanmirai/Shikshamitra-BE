using System;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.Finance.Setting.ConcessionType;

public class ConcessionTypeFeeHead : Entity<Guid> // Composite key usually, but ABP recommends Entity<Guid> or overriding Keys
{
    public Guid ConcessionTypeId { get; private set; }
    public Guid FeeHeadId { get; private set; }

    // Navigation properties
    // public virtual ConcessionType ConcessionType { get; set; }
    // public virtual FeeHead FeeHead { get; set; }

    private ConcessionTypeFeeHead()
    {
        /* This constructor is for deserialization / ORM purpose */
    }

    public ConcessionTypeFeeHead(Guid concessionTypeId, Guid feeHeadId)
    {
        ConcessionTypeId = concessionTypeId;
        FeeHeadId = feeHeadId;
    }

    public override object[] GetKeys()
    {
        return new object[] { ConcessionTypeId, FeeHeadId };
    }
}
