using ShikshaMitra.Finance.Fees.Settings.PaymentTerms;
using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Domain.Finance.Fees.Settings.PaymentTerms;

/// <summary>
/// A normalized child table for structure-specific date/percentage overrides on a Payment Term schedule.
/// Exists only for <see cref="ScheduleMode.Customized"/> fee-head attachments.
/// Cannot alter the master template; validation creates a complete resolved schedule.
/// </summary>
public class FeeStructureDetailScheduleOverride : FullAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }

	public Guid FeeStructureDetailId { get; private set; }

	/// <summary>
	/// FK to the master installment being overridden.
	/// </summary>
	public Guid SourcePaymentTermInstallmentId { get; private set; }

	/// <summary>Snapshot of the master installment's sequence.</summary>
	public int Sequence { get; private set; }

	/// <summary>Snapshot of the master installment's name.</summary>
	public string Name { get; private set; } = string.Empty;

	/// <summary>Overridden coverage start date (null = inherit master).</summary>
	public DateTime? CoverageStartDate { get; private set; }

	/// <summary>Overridden coverage end date (null = inherit master).</summary>
	public DateTime? CoverageEndDate { get; private set; }

	/// <summary>Overridden demand date (null = inherit master).</summary>
	public DateTime? DemandDate { get; private set; }

	/// <summary>Overridden due date (null = inherit master).</summary>
	public DateTime? DueDate { get; private set; }

	/// <summary>Overridden allocation percentage (null = inherit master).</summary>
	public decimal? AllocationPercentage { get; private set; }

	protected FeeStructureDetailScheduleOverride() { /* For ORM */ }

	public FeeStructureDetailScheduleOverride(
		Guid id,
		Guid? tenantId,
		Guid feeStructureDetailId,
		Guid sourcePaymentTermInstallmentId,
		int sequence,
		string name,
		DateTime? coverageStartDate = null,
		DateTime? coverageEndDate = null,
		DateTime? demandDate = null,
		DateTime? dueDate = null,
		decimal? allocationPercentage = null) : base(id)
	{
		TenantId = tenantId;
		FeeStructureDetailId = feeStructureDetailId;
		SourcePaymentTermInstallmentId = sourcePaymentTermInstallmentId;
		Sequence = sequence;
		Name = name;
		CoverageStartDate = coverageStartDate?.Date;
		CoverageEndDate = coverageEndDate?.Date;
		DemandDate = demandDate?.Date;
		DueDate = dueDate?.Date;
		AllocationPercentage = allocationPercentage;
	}

	public void Update(
		DateTime? coverageStartDate = null,
		DateTime? coverageEndDate = null,
		DateTime? demandDate = null,
		DateTime? dueDate = null,
		decimal? allocationPercentage = null)
	{
		CoverageStartDate = coverageStartDate?.Date;
		CoverageEndDate = coverageEndDate?.Date;
		DemandDate = demandDate?.Date;
		DueDate = dueDate?.Date;
		AllocationPercentage = allocationPercentage;
	}
}
