using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace ShikshaMitra.Finance.ConcessionRequests;

public class ConcessionRequest : FullAuditedAggregateRoot<Guid>
{
    public Guid? TenantId { get; set; }

    protected ConcessionRequest()
    {
    }

    public ConcessionRequest(Guid id) : base(id)
    {
    }
}
