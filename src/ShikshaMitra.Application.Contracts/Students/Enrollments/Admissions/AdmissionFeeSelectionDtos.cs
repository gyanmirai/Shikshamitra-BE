using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionFeeSelectionHeadDto
{
	public Guid FeeHeadId { get; set; }
	public string FeeHeadName { get; set; } = string.Empty;
	public decimal? ConfiguredAmount { get; set; }
	public bool IsVariable { get; set; }
	public bool IsOptional { get; set; }
	public bool IsOneTime { get; set; }
	public Guid? PaymentTermId { get; set; }
	public Guid? PaymentTermVersionId { get; set; }
}

public class AdmissionFeeStructureCandidateDto
{
	public Guid StructureId { get; set; }
	public string StructureName { get; set; } = string.Empty;
	public int RevisionNumber { get; set; }
	public Guid AcademicYearId { get; set; }
	public Guid StandardId { get; set; }
	public List<AdmissionFeeSelectionHeadDto> Heads { get; set; } = [];
}

public class AdmissionFeeSelectionSnapshotDto : AdmissionFeeStructureCandidateDto
{
	public string? Currency { get; set; }
	public Guid SavedBy { get; set; }
	public DateTimeOffset SavedAt { get; set; }
	public DateTimeOffset? OnboardingConfirmedAt { get; set; }
}

public class AdmissionFeeSelectionDto
{
	public Guid ApplicationId { get; set; }
	public string ConcurrencyStamp { get; set; } = string.Empty;
	public Guid? OwnerStageId { get; set; }
	public string? OwnerStageName { get; set; }
	public AdmissionWorkflowStageType? OwnerStageType { get; set; }
	public Guid? CurrentStageId { get; set; }
	public string? Currency { get; set; }
	public bool Available { get; set; }
	public bool IsRequired { get; set; }
	public bool IsLocked { get; set; }
	public bool HasRecordedFeePayment { get; set; }
	public AdmissionPaymentCompletionDto? PaymentCompletion { get; set; }
	public AdmissionFeeCollectionDeferralDto? FeeCollectionDeferral { get; set; }
	public bool CanEdit { get; set; }
	public bool CanConfirmOnboarding { get; set; }
	public bool OnboardingConfirmed { get; set; }
	public bool AutomaticallyConfirmedFromAdmissions { get; set; }
	public string? OnboardingBlocker { get; set; }
	public AdmissionFeeSelectionSnapshotDto? Selection { get; set; }
}

public class AdmissionPaymentCompletionDto
{
	public string PaymentRule { get; set; } = "partial";
	public bool AllowCollectionLater { get; set; } = true;
	public bool CanComplete { get; set; }
	public bool CanDeferCollection { get; set; }
	public string Status { get; set; } = "collectionPending";
	public string? Blocker { get; set; }
}

public class AdmissionFeeCollectionDeferralDto
{
	public Guid StageId { get; set; }
	public string Reason { get; set; } = string.Empty;
	public Guid DeferredBy { get; set; }
	public DateTimeOffset DeferredAt { get; set; }
}

public class SaveAdmissionFeeSelectionDto
{
	public Guid StructureId { get; set; }
	[MaxLength(100)] public List<Guid> OptionalHeadIds { get; set; } = [];
	[Required, MaxLength(40)] public string ConcurrencyStamp { get; set; } = string.Empty;
}

public class ConfirmAdmissionFeeSelectionDto
{
	[Required, MaxLength(40)] public string ConcurrencyStamp { get; set; } = string.Empty;
}

public interface IAdmissionFeeSelectionAppService : IApplicationService
{
	Task<AdmissionFeeSelectionDto> GetAsync(Guid applicationId);
	Task<List<AdmissionFeeStructureCandidateDto>> GetCandidatesAsync(Guid applicationId);
	Task<AdmissionFeeSelectionDto> SaveAsync(Guid applicationId, SaveAdmissionFeeSelectionDto input);
	Task<AdmissionFeeSelectionDto> ConfirmOnboardingAsync(Guid applicationId, ConfirmAdmissionFeeSelectionDto input);
}
