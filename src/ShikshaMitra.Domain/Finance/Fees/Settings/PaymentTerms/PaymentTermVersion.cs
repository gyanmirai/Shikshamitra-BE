using ShikshaMitra.Finance.Fees.Settings.PaymentTerms;
using System;
using System.Collections.Generic;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

// Alias to avoid namespace shadowing between Domain and Domain.Shared layers
using ErrorCodes = ShikshaMitra.Finance.Fees.Settings.PaymentTerms.PaymentTermErrorCodes;

namespace ShikshaMitra.Domain.Finance.Fees.Settings.PaymentTerms;

/// <summary>
/// Immutable snapshot of a <see cref="PaymentTerm"/> at a point in time.
/// Each publish creates a new version; existing versions are never mutated.
/// </summary>
public class PaymentTermVersion : CreationAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }

	public Guid PaymentTermId { get; private set; }

	/// <summary>Sequential version number (1-based).</summary>
	public int VersionNumber { get; private set; }

	public PaymentTermVersionStatus Status { get; private set; }

	public HolidayShiftingMethod HolidayShiftingMethod { get; private set; }

	public DemandGenerationRule DemandGenerationRule { get; private set; }

	public int? DemandOffsetDays { get; private set; }

	public DateTime? EffectiveFrom { get; private set; }

	public DateTime? EffectiveTo { get; private set; }

	public DateTime? PublishedAt { get; private set; }

	public Guid? PublishedBy { get; private set; }

	public string? ChangeReason { get; private set; }

	/// <summary>
	/// Optional hash of the installment schedule, used to detect identical revisions.
	/// </summary>
	public string? SnapshotHash { get; private set; }

	public ICollection<PaymentTermInstallment> Installments { get; private set; } = new List<PaymentTermInstallment>();

	protected PaymentTermVersion() { /* For ORM */ }

	public PaymentTermVersion(
		Guid id,
		Guid? tenantId,
		Guid paymentTermId,
		int versionNumber,
		PaymentTermVersionStatus status,
		HolidayShiftingMethod holidayShiftingMethod,
		DemandGenerationRule demandGenerationRule,
		int? demandOffsetDays,
		DateTime? effectiveFrom,
		DateTime? effectiveTo,
		string? changeReason = null,
		string? snapshotHash = null) : base(id)
	{
		TenantId = tenantId;
		PaymentTermId = paymentTermId;
		VersionNumber = versionNumber;
		Status = status;
		HolidayShiftingMethod = holidayShiftingMethod;
		DemandGenerationRule = demandGenerationRule;
		DemandOffsetDays = demandOffsetDays;
		EffectiveFrom = effectiveFrom?.Date;
		EffectiveTo = effectiveTo?.Date;
		ChangeReason = string.IsNullOrWhiteSpace(changeReason) ? null : changeReason.Trim();
		SnapshotHash = snapshotHash;
	}

	public void UpdateBillingConfiguration(
		HolidayShiftingMethod holidayShiftingMethod,
		DemandGenerationRule demandGenerationRule,
		int? demandOffsetDays)
	{
		if (Status != PaymentTermVersionStatus.Draft)
		{
			throw new BusinessException(ErrorCodes.VersionNotEditable)
				.WithData("VersionNumber", VersionNumber)
				.WithData("Status", Status.ToString());
		}

		HolidayShiftingMethod = holidayShiftingMethod;
		DemandGenerationRule = demandGenerationRule;
		DemandOffsetDays = demandOffsetDays;
	}

	/// <summary>
	/// Replaces all installments. Only valid while <see cref="Status"/> is <see cref="PaymentTermVersionStatus.Draft"/>.
	/// </summary>
	public void SetInstallments(IReadOnlyList<PaymentTermInstallment> installments)
	{
		if (Status != PaymentTermVersionStatus.Draft)
		{
			throw new BusinessException(ErrorCodes.VersionNotEditable)
				.WithData("VersionNumber", VersionNumber)
				.WithData("Status", Status.ToString());
		}

		Installments.Clear();
		foreach (var installment in installments)
		{
			Installments.Add(installment);
		}
	}

	/// <summary>
	/// Transitions this version to Published after validation.
	/// </summary>
	public void Publish(DateTime effectiveFrom, Guid publishedBy)
	{
		if (Status != PaymentTermVersionStatus.Draft)
		{
			throw new BusinessException(ErrorCodes.VersionNotPublishable)
				.WithData("VersionNumber", VersionNumber)
				.WithData("Status", Status.ToString());
		}

		Status = PaymentTermVersionStatus.Published;
		EffectiveFrom = effectiveFrom.Date;
		PublishedAt = DateTime.UtcNow;
		PublishedBy = publishedBy;
	}

	/// <summary>
	/// Marks this version as superseded by a newer published version.
	/// </summary>
	public void Supersede(DateTime? effectiveTo)
	{
		Status = PaymentTermVersionStatus.Superseded;
		EffectiveTo = effectiveTo?.Date;
	}

	/// <summary>
	/// Archives this version. Used when the parent term is archived.
	/// </summary>
	public void Archive()
	{
		Status = PaymentTermVersionStatus.Archived;
	}
}
