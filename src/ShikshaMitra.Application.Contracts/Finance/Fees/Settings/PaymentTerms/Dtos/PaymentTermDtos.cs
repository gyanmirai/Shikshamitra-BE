using ShikshaMitra.Finance.Fees.Settings.PaymentTerms;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Finance.Fees.Settings.PaymentTerms.Dtos;

// ── Installment DTOs ──────────────────────────────────────────────────────────

public class PaymentTermInstallmentDto : EntityDto<Guid>
{
	public int Sequence { get; set; }
	public string Name { get; set; } = string.Empty;
	public DateTime CoverageStartDate { get; set; }
	public DateTime CoverageEndDate { get; set; }
	public DateTime DemandDate { get; set; }
	public DateTime DueDate { get; set; }
	public decimal AllocationPercentage { get; set; }
	public bool DemandEnabled { get; set; }
	public bool ReminderEnabled { get; set; }
	public DateTime? ReminderDate { get; set; }
	public bool? AllowPartialOverride { get; set; }
	public int? GracePeriodDaysOverride { get; set; }
}

public class CreatePaymentTermInstallmentDto
{
	[Range(1, int.MaxValue)]
	public int Sequence { get; set; }
	[Required, StringLength(100)]
	public string Name { get; set; } = string.Empty;
	[JsonConverter(typeof(PaymentTermCalendarDateJsonConverter))]
	public DateTime CoverageStartDate { get; set; }
	[JsonConverter(typeof(PaymentTermCalendarDateJsonConverter))]
	public DateTime CoverageEndDate { get; set; }
	[JsonConverter(typeof(PaymentTermNullableCalendarDateJsonConverter))]
	public DateTime? DemandDate { get; set; }
	[JsonConverter(typeof(PaymentTermCalendarDateJsonConverter))]
	public DateTime DueDate { get; set; }
	[Range(typeof(decimal), "0.01", "100")]
	public decimal AllocationPercentage { get; set; }
	public bool DemandEnabled { get; set; }
	public bool ReminderEnabled { get; set; }
	[JsonConverter(typeof(PaymentTermNullableCalendarDateJsonConverter))]
	public DateTime? ReminderDate { get; set; }
	public bool? AllowPartialOverride { get; set; }
	public int? GracePeriodDaysOverride { get; set; }
}

// ── Version DTOs ──────────────────────────────────────────────────────────────

public class PaymentTermVersionDto : EntityDto<Guid>
{
	public int VersionNumber { get; set; }
	public PaymentTermVersionStatus Status { get; set; }
	public HolidayShiftingMethod HolidayShiftingMethod { get; set; }
	public DemandGenerationRule DemandGenerationRule { get; set; }
	public int? DemandOffsetDays { get; set; }
	public DateTime? EffectiveFrom { get; set; }
	public DateTime? EffectiveTo { get; set; }
	public DateTime? PublishedAt { get; set; }
	public string? ChangeReason { get; set; }
	public List<PaymentTermInstallmentDto> Installments { get; set; } = new();
}

/// <summary>
/// Lightweight audit projection used by the payment-term version-history drawer.
/// The full version snapshot remains available from <see cref="PaymentTermDto.Versions"/>.
/// </summary>
public class PaymentTermVersionHistoryDto : EntityDto<Guid>
{
	public int VersionNumber { get; set; }
	public PaymentTermVersionStatus Status { get; set; }
	public string ChangeAction { get; set; } = string.Empty;
	public string? ChangeReason { get; set; }
	public DateTime CreationTime { get; set; }
	public Guid? CreatorId { get; set; }
	public string? CreatedByName { get; set; }
}

// ── Master DTOs ───────────────────────────────────────────────────────────────

public class PaymentTermDto : EntityDto<Guid>
{
	public Guid AcademicYearId { get; set; }
	public string? AcademicYearName { get; set; }
	public Guid BoardId { get; set; }
	public string? BoardName { get; set; }
	public string Name { get; set; } = string.Empty;
	public string Code { get; set; } = string.Empty;
	public string? Description { get; set; }
	public PaymentTermLifecycleStatus Status { get; set; }
	public bool IsDefault { get; set; }

	public HolidayShiftingMethod HolidayShiftingMethod { get; set; }
	public DemandGenerationRule DemandGenerationRule { get; set; }
	public int? DemandOffsetDays { get; set; }
	public InitialDemandStatus InitialDemandStatus { get; set; }

	public Guid? CurrentVersionId { get; set; }
	public DateTime? ArchivedAt { get; set; }

	public int TotalInstallments { get; set; }
	public int VersionCount { get; set; }
	public bool IsUsedInFeeStructures { get; set; }
	public bool CanDirectEdit { get; set; }

	public List<PaymentTermVersionDto> Versions { get; set; } = new();
}

public class PaymentTermListDto : EntityDto<Guid>
{
	public Guid? PublishedVersionId { get; set; }
	public int? PublishedVersionNumber { get; set; }
	public string Name { get; set; } = string.Empty;
	public string Code { get; set; } = string.Empty;
	public Guid AcademicYearId { get; set; }
	public Guid BoardId { get; set; }
	public PaymentTermLifecycleStatus Status { get; set; }
	public bool IsDefault { get; set; }
	public int CurrentVersionNumber { get; set; }
	public bool HasDraft { get; set; }
	public bool HasPublishedVersion { get; set; }
	public int InstallmentCount { get; set; }
	public int ReferencedStructureCount { get; set; }
	public bool CanDirectEdit { get; set; }
}

// ── Request DTOs ──────────────────────────────────────────────────────────────

public class CreatePaymentTermDto
{
	public Guid AcademicYearId { get; set; }
	public Guid BoardId { get; set; }
	[Required, StringLength(100, MinimumLength = 3)]
	public string Name { get; set; } = string.Empty;
	[Required, StringLength(24, MinimumLength = 3)]
	[RegularExpression("^[A-Z0-9-]+$")]
	public string Code { get; set; } = string.Empty;
	[StringLength(500)]
	public string? Description { get; set; }
	public bool IsDefault { get; set; }
	public PaymentTermSaveStatus Status { get; set; } = PaymentTermSaveStatus.Draft;

	public HolidayShiftingMethod HolidayShiftingMethod { get; set; } = HolidayShiftingMethod.None;
	public DemandGenerationRule DemandGenerationRule { get; set; } = DemandGenerationRule.OnInstallmentStartDate;
	public int? DemandOffsetDays { get; set; }

	public List<CreatePaymentTermInstallmentDto> Installments { get; set; } = new();
}

public class UpdatePaymentTermDraftDto
{
	[Required, StringLength(100, MinimumLength = 3)]
	public string Name { get; set; } = string.Empty;
	[Required, StringLength(24, MinimumLength = 3)]
	[RegularExpression("^[A-Z0-9-]+$")]
	public string Code { get; set; } = string.Empty;
	[StringLength(500)]
	public string? Description { get; set; }
	public bool IsDefault { get; set; }
	public PaymentTermSaveStatus Status { get; set; } = PaymentTermSaveStatus.Draft;

	public HolidayShiftingMethod HolidayShiftingMethod { get; set; }
	public DemandGenerationRule DemandGenerationRule { get; set; }
	public int? DemandOffsetDays { get; set; }

	public List<CreatePaymentTermInstallmentDto> Installments { get; set; } = new();
}

public class CreateRevisionDto
{
	[Required, StringLength(500)]
	public string ChangeReason { get; set; } = string.Empty;
	public Guid? SourceVersionId { get; set; }
}

public class ClonePaymentTermsDto
{
	public Guid SourceAcademicYearId { get; set; }
	public Guid TargetAcademicYearId { get; set; }
	public List<Guid> PaymentTermIds { get; set; } = new();
}

public class PreviewPaymentTermTemplateDto
{
	public Guid BoardId { get; set; }
	public PaymentTermTemplateKind TemplateKind { get; set; }
	[Range(1, 36)]
	public int? CustomInstallmentCount { get; set; }
	public PaymentTermTemplateDueDateRule DueDateRule { get; set; } = PaymentTermTemplateDueDateRule.CoverageEnd;
	[Range(0, 365)]
	public int? DueDaysAfterCoverageStart { get; set; }
	public DemandGenerationRule DemandGenerationRule { get; set; } = DemandGenerationRule.OnInstallmentStartDate;
	[Range(1, 365)]
	public int? DemandOffsetDays { get; set; }
}

public class ArchivePaymentTermDto
{
	[Required, StringLength(500)]
	public string Reason { get; set; } = string.Empty;
}

// ── Response DTOs ─────────────────────────────────────────────────────────────

public class ClonePaymentTermsResultDto
{
	public List<Guid> ClonedTermIds { get; set; } = new();
	public List<string> ConflictedCodes { get; set; } = new();
	public List<string> DateRangesRequiringReview { get; set; } = new();
}

public class PaymentTermTemplatePreviewDto
{
	public PaymentTermTemplateKind TemplateKind { get; set; }
	public DateTime BoardStartDate { get; set; }
	public DateTime BoardEndDate { get; set; }
	public int InstallmentCount { get; set; }
	public List<CreatePaymentTermInstallmentDto> Installments { get; set; } = new();
}

public class ImpactPreviewDto
{
	public int AffectedStructures { get; set; }
	public int AffectedStudents { get; set; }
	public int ExistingIssuedDueCount { get; set; }
	public int FutureUngeneratedInstanceCount { get; set; }
	public decimal ProjectedTotalAmount { get; set; }
	public List<ImpactPreviewStructureDto> Structures { get; set; } = new();
}

public class ImpactPreviewStructureDto
{
	public Guid FeeStructureId { get; set; }
	public string FeeStructureName { get; set; } = string.Empty;
	public int StudentCount { get; set; }
	public int IssuedDueCount { get; set; }
	public int FutureInstanceCount { get; set; }
	public decimal ProjectedAmount { get; set; }
}

public class PublishResultDto
{
	public Guid VersionId { get; set; }
	public int VersionNumber { get; set; }
	public List<string> ValidationWarnings { get; set; } = new();
	public ImpactPreviewDto? ImpactPreview { get; set; }
}

// ── Query DTOs ────────────────────────────────────────────────────────────────

public class PaymentTermGetListInput : PagedAndSortedResultRequestDto
{
	public Guid AcademicYearId { get; set; }
	public Guid? BoardId { get; set; }
	public PaymentTermStatus? Status { get; set; }
	public string? Search { get; set; }
	public bool IncludeArchived { get; set; }
}
