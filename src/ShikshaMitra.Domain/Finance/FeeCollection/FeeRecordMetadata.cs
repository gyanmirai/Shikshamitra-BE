using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.FeeCollection;

public class FeeRecordMetadata : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; set; }
	public Guid StudentId { get; set; }
	public double TotalPaidFee { get; set; }
	public string? Remarks { get; set; }
}
