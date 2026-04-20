using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.FeeCollection;

public class FeeCollectionRecord : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; set; }
	public Guid StudentId { get; set; }
	public Guid FeeHeadId { get; set; }
	public double Amount { get; set; }
	public DateTimeOffset PaymentDate { get; set; }
	public Guid FeeRecordMetadataId { get; set; }
}
