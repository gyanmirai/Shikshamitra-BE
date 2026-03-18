using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.ConcessionRequests;

public class ConcessionRequest : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

	public Guid StudentId { get; set; }
	public Guid ConcessionTypeId { get; set; }
	public ConcessionRequestStatus Status { get; set; }
	public ICollection<ConcessionFeeHeadMapping> ConcessionFeeHeadMappings { get; set; } = new List<ConcessionFeeHeadMapping>();

	protected ConcessionRequest()
    {
    }

    public ConcessionRequest(Guid id) : base(id)
    {
    }
}
