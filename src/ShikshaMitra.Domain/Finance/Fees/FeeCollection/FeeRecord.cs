using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Fees.FeeCollection;

public class FeeCollectionRecord : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	private FeeCollectionRecord() { /* For ORM */ }

	public FeeCollectionRecord(Guid id) : base(id) { }

	public Guid? TenantId { get; set; }
	public Guid StudentId { get; set; }
	public Guid FeeHeadId { get; set; }
	public double Amount { get; set; }
	public DateTimeOffset PaymentDate { get; set; }
	public Guid FeeRecordMetadataId { get; set; }
}
