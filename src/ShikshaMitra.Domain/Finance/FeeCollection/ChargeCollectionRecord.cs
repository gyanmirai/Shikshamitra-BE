using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.FeeCollection;

public class ChargeCollectionRecord : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	private ChargeCollectionRecord() { /* For ORM */ }

	public ChargeCollectionRecord(Guid id) : base(id) { }

	public Guid? TenantId { get; set; }
	public Guid StudentId { get; set; }
	public Guid ChargeId { get; set; }
	public double Amount { get; set; }
	public DateTimeOffset PaymentDate { get; set; }
	public Guid FeeRecordMetadataId { get; set; }
}
