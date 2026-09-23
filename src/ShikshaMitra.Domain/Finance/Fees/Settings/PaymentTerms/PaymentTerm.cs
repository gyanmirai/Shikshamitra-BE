using ShikshaMitra.Finance.Fees.Settings.PaymentTerms;
using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

using ErrorCodes = ShikshaMitra.Finance.Fees.Settings.PaymentTerms.PaymentTermErrorCodes;

namespace ShikshaMitra.Domain.Finance.Fees.Settings.PaymentTerms;

/// <summary>
/// Schedule-template aggregate for fee-head installment calendars.
/// Payment behaviour (partial pay, modes, reminders) lives on Payment Policy — not here.
/// </summary>
public class PaymentTerm : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }

	public Guid AcademicYearId { get; private set; }

	public Guid BoardId { get; private set; }

	public string Name { get; private set; } = string.Empty;

	public string Code { get; private set; } = string.Empty;

	public string? Description { get; private set; }

	public PaymentTermStatus Status { get; private set; }

	public bool IsDefault { get; private set; }

	public HolidayShiftingMethod HolidayShiftingMethod { get; private set; }

	public DemandGenerationRule DemandGenerationRule { get; private set; }

	public int? DemandOffsetDays { get; private set; }

	public InitialDemandStatus InitialDemandStatus { get; private set; }

	/// <summary>Pointer to the current published (or only draft) version.</summary>
	public Guid? CurrentVersionId { get; private set; }

	public DateTime? ArchivedAt { get; private set; }

	public string? ArchiveReason { get; private set; }

	public ICollection<PaymentTermVersion> Versions { get; private set; } = new List<PaymentTermVersion>();

	protected PaymentTerm() { /* For ORM */ }

	public PaymentTerm(
		Guid id,
		Guid? tenantId,
		Guid academicYearId,
		Guid boardId,
		string name,
		string code,
		string? description,
		bool isDefault,
		HolidayShiftingMethod holidayShiftingMethod,
		DemandGenerationRule demandGenerationRule,
		int? demandOffsetDays,
		InitialDemandStatus initialDemandStatus) : base(id)
	{
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		BoardId = boardId;
		SetIdentity(name, code, description);
		if (isDefault)
		{
			throw new BusinessException(ErrorCodes.DefaultTermMustBeActive);
		}
		IsDefault = false;
		HolidayShiftingMethod = holidayShiftingMethod;
		DemandGenerationRule = demandGenerationRule;
		DemandOffsetDays = demandOffsetDays;
		InitialDemandStatus = initialDemandStatus;
		Status = PaymentTermStatus.Active;
	}

	public PaymentTermVersion? GetCurrentVersion()
	{
		if (CurrentVersionId.HasValue)
		{
			return Versions.FirstOrDefault(v => v.Id == CurrentVersionId.Value);
		}

		return Versions
			.OrderByDescending(v => v.VersionNumber)
			.FirstOrDefault();
	}

	public PaymentTermVersion? GetDraftVersion()
		=> Versions.FirstOrDefault(v => v.Status == PaymentTermVersionStatus.Draft);

	public PaymentTermVersion? GetPublishedVersion()
		=> Versions
			.Where(v => v.Status == PaymentTermVersionStatus.Published)
			.OrderByDescending(v => v.VersionNumber)
			.FirstOrDefault();

	public void UpdateMetadata(
		string name,
		string code,
		string? description,
		HolidayShiftingMethod holidayShiftingMethod,
		DemandGenerationRule demandGenerationRule,
		int? demandOffsetDays,
		InitialDemandStatus initialDemandStatus)
	{
		EnsureActive();
		SetIdentity(name, code, description);
		HolidayShiftingMethod = holidayShiftingMethod;
		DemandGenerationRule = demandGenerationRule;
		DemandOffsetDays = demandOffsetDays;
		InitialDemandStatus = initialDemandStatus;
	}

	public void SetDefault(bool isDefault)
	{
		if (isDefault && (Status == PaymentTermStatus.Archived || GetPublishedVersion() == null))
		{
			throw new BusinessException(ErrorCodes.DefaultTermMustBeActive);
		}

		IsDefault = isDefault;
	}

	public void AddVersion(PaymentTermVersion version)
	{
		Versions.Add(version);
		// CurrentVersionId points at the assignable (published) version — never a draft.
		if (!CurrentVersionId.HasValue && version.Status == PaymentTermVersionStatus.Published)
		{
			CurrentVersionId = version.Id;
		}
	}

	public void SetCurrentVersion(Guid versionId)
	{
		if (Versions.All(v => v.Id != versionId))
		{
			throw new BusinessException(ErrorCodes.NotFound)
				.WithData("VersionId", versionId);
		}

		CurrentVersionId = versionId;
	}

	public void Archive(string reason)
	{
		if (string.IsNullOrWhiteSpace(reason))
		{
			throw new BusinessException(ErrorCodes.ArchiveReasonRequired);
		}

		Status = PaymentTermStatus.Archived;
		ArchivedAt = DateTime.UtcNow;
		ArchiveReason = reason.Trim();
		IsDefault = false;

		// Version snapshots keep their lifecycle state. Archiving the master only
		// removes it from new use; restore can therefore re-enable the same audited
		// published schedule without mutating history.
	}

	public void Restore()
	{
		Status = PaymentTermStatus.Active;
		ArchivedAt = null;
		ArchiveReason = null;
	}

	private void EnsureActive()
	{
		if (Status == PaymentTermStatus.Archived)
		{
			throw new BusinessException(ErrorCodes.ArchiveInUse)
				.WithData("PaymentTermId", Id);
		}
	}

	private void SetIdentity(string name, string code, string? description)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			throw new BusinessException(ErrorCodes.NameRequired);
		}

		if (string.IsNullOrWhiteSpace(code))
		{
			throw new BusinessException(ErrorCodes.CodeRequired);
		}

		Name = name.Trim();
		Code = code.Trim().ToUpperInvariant();
		Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
	}
}
