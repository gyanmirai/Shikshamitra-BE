using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.FeeCollection;

/// <summary>
/// Represents the top-level metadata for a single fee collection transaction.
/// One record is created per collection event, linking all fee-head payments,
/// charge payments, and payment-method splits together.
/// </summary>
public class FeeRecordMetadata : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	private FeeRecordMetadata() { /* For ORM */ }

	public FeeRecordMetadata(Guid id) : base(id) { }

	public Guid? TenantId { get; set; }
	public Guid StudentId { get; set; }
	public Guid AcademicYearId { get; set; }
	public double TotalPaidFee { get; set; }

	/// <summary>Unique receipt number formatted as REC-{YYYY}-{NNNNNN}.</summary>
	public string ReceiptNumber { get; set; } = string.Empty;

	/// <summary>Payment status of this collection record.</summary>
	public FeeCollectionStatus Status { get; set; }

	public string? Remarks { get; set; }
}
