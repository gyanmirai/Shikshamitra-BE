using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmSettingsAccessDto
{
	public string SettingsKey { get; set; } = string.Empty;
	public string? SectionKey { get; set; }
	public AdmissionCrmSettingsAccessMode AccessMode { get; set; }
}

public class UpdateAdmissionCrmSettingsAccessDto
{
	[Required, MaxLength(64)] public string SettingsKey { get; set; } = string.Empty;
	[MaxLength(64)] public string? SectionKey { get; set; }
	public AdmissionCrmSettingsAccessMode AccessMode { get; set; }
}

public class AdmissionCrmSetupItemDto
{
	public string Key { get; set; } = string.Empty;
	public string Title { get; set; } = string.Empty;
	public AdmissionCrmSetupReadiness Status { get; set; }
	public AdmissionCrmSettingsAccessMode AccessMode { get; set; }
	public IReadOnlyList<string> BlockerCodes { get; set; } = [];
	public string Path { get; set; } = string.Empty;
}

public class AdmissionCrmSetupStatusDto
{
	public Guid AcademicYearId { get; set; }
	public string CycleName { get; set; } = string.Empty;
	public AdmissionCrmSetupReadiness Status { get; set; }
	public string? AppliedPresetName { get; set; }
	public int? AppliedPresetVersion { get; set; }
	public DateTime? LastUpdatedAt { get; set; }
	public IReadOnlyList<AdmissionCrmSetupItemDto> Items { get; set; } = [];
}

public class AdmissionCrmCycleRequiredActionDto
{
	public string Key { get; set; } = string.Empty;
	public string Title { get; set; } = string.Empty;
	public string Path { get; set; } = string.Empty;
	public IReadOnlyList<string> BlockerCodes { get; set; } = [];
}

public class AdmissionCrmCycleListItemDto
{
	public Guid AcademicYearId { get; set; }
	public Guid? CycleConfigurationId { get; set; }
	public string CycleName { get; set; } = string.Empty;
	public string AcademicYearName { get; set; } = string.Empty;
	public DateTime AcademicYearStartDate { get; set; }
	public DateTime AcademicYearEndDate { get; set; }
	public DateTime? AdmissionWindowStart { get; set; }
	public DateTime? AdmissionWindowEnd { get; set; }
	public AdmissionCrmCycleStatus Status { get; set; }
	public AdmissionCrmSetupReadiness Readiness { get; set; }
	public int ApplicantCount { get; set; }
	public Guid? OwnerUserId { get; set; }
	public string? OwnerDisplayName { get; set; }
	public DateTime? LastUpdatedAt { get; set; }
	public IReadOnlyList<AdmissionCrmCycleRequiredActionDto> RequiredActions { get; set; } = [];
}

public class AdmissionCrmCyclePolicySummaryDto
{
	public bool IsActive { get; set; }
	public bool RegistrationFormRequiredBeforeExam { get; set; }
	public bool EntranceExamRequired { get; set; }
	public bool JourneyLocked { get; set; }
	public int DefaultLeadSlaHours { get; set; }
	public int StaleAfterDays { get; set; }
	public int ArchiveAfterDays { get; set; }
	public decimal DefaultExamPassingPercentage { get; set; }
	public AdmissionCrmMeritRankingMode MeritRankingMode { get; set; }
}

public class AdmissionCrmCycleSummaryDto
{
	public AdmissionCrmCycleListItemDto Cycle { get; set; } = new();
	public AdmissionCrmCyclePolicySummaryDto Policy { get; set; } = new();
	public AdmissionCrmCycleCommunicationSummaryDto Communications { get; set; } = new();
	public AdmissionCrmSetupStatusDto Readiness { get; set; } = new();
	public AdmissionCrmApplicationStagePolicyDto ApplicationStagePolicy { get; set; } = new();
}

public class AdmissionCrmCycleCommunicationSummaryDto
{
	public int JourneyRuleCount { get; set; }
	public int ActiveMessageRuleCount { get; set; }
	public int TemplateBackedRuleCount { get; set; }
}

public class AdmissionCrmAdmissionYearSetupDto : EntityDto<Guid>
{
	public string ConcurrencyStamp { get; set; } = string.Empty;
	public Guid AcademicYearId { get; set; }
	public int Version { get; set; }
	public AdmissionCrmAdmissionYearSetupStatus Status { get; set; }
	public DateTime InquiryOpenDate { get; set; }
	public DateTime InquiryCloseDate { get; set; }
	public DateTime RegistrationCloseDate { get; set; }
	public DateTime ActivationTargetDate { get; set; }
	public bool EntranceExamRequired { get; set; }
	public bool FinanceVerificationRequired { get; set; }
	public AdmissionCrmPlacementTiming PlacementTiming { get; set; }
	public Guid? DefaultOwnerUserId { get; set; }
	public IReadOnlyList<Guid> TargetStandardIds { get; set; } = [];
	public IReadOnlyList<AdmissionCrmRouteType> RouteTypes { get; set; } = [];
	public IReadOnlyDictionary<string, Guid?> StepOwners { get; set; } =
		new Dictionary<string, Guid?>();
	public DateTime? PublishedAt { get; set; }
	public Guid? PublishedByUserId { get; set; }
	public DateTime? ClosedAt { get; set; }
	public Guid? ClosedByUserId { get; set; }
	public string? CloseReason { get; set; }
}

public class UpdateAdmissionCrmAdmissionYearSetupDto
{
	[Required]
	public string ConcurrencyStamp { get; set; } = string.Empty;
	public DateTime InquiryOpenDate { get; set; }
	public DateTime InquiryCloseDate { get; set; }
	public DateTime RegistrationCloseDate { get; set; }
	public DateTime ActivationTargetDate { get; set; }
	public bool EntranceExamRequired { get; set; }
	public bool FinanceVerificationRequired { get; set; }
	public AdmissionCrmPlacementTiming PlacementTiming { get; set; }
	public Guid? DefaultOwnerUserId { get; set; }
	public IReadOnlyList<Guid> TargetStandardIds { get; set; } = [];
	public IReadOnlyList<AdmissionCrmRouteType> RouteTypes { get; set; } = [];
	public IReadOnlyDictionary<string, Guid?> StepOwners { get; set; } =
		new Dictionary<string, Guid?>();
}

public class CloseAdmissionCrmAdmissionYearSetupDto
{
	[Required]
	public string ConcurrencyStamp { get; set; } = string.Empty;
	[Required, MaxLength(AdmissionCrmConsts.MaxNotesLength)]
	public string Reason { get; set; } = string.Empty;
}

public class PublishAdmissionCrmAdmissionYearSetupDto
{
	[Required]
	public string ConcurrencyStamp { get; set; } = string.Empty;
}

public class AdmissionCrmAdmissionYearStandardOptionDto
{
	public Guid StandardId { get; set; }
	public string StandardName { get; set; } = string.Empty;
	public Guid MediumStageId { get; set; }
	public string MediumStageName { get; set; } = string.Empty;
	public Guid MediumId { get; set; }
	public string MediumName { get; set; } = string.Empty;
	public Guid BoardId { get; set; }
	public string BoardName { get; set; } = string.Empty;
}

public class AdmissionCrmApplicationStagePolicyDto
{
	public int Version { get; set; } = 1;
	public bool EntranceExamRequired { get; set; }
	public bool JourneyLocked { get; set; }
	public int ApplicantCount { get; set; }
	public string ImpactFingerprint { get; set; } = string.Empty;
	public IReadOnlyList<AdmissionCrmApplicationStagePolicyItemDto> Stages { get; set; } = [];
}

public class AdmissionCrmApplicationStagePolicyItemDto
{
	public AdmissionCrmApplicationStageKey StageKey { get; set; }
	public string DisplayName { get; set; } = string.Empty;
	public int DisplayOrder { get; set; }
	public bool IsEnabled { get; set; }
	public bool IsRequired { get; set; }
	public bool IsSystemLocked { get; set; }
	public bool IsExamStage { get; set; }
	public bool IsOperationallyBacked { get; set; }
	public IReadOnlyList<AdmissionInquiryStage> SupportedOperationalStages { get; set; } = [];
	public string ReadinessNote { get; set; } = string.Empty;
}

public class UpdateAdmissionCrmApplicationStagePolicyDto
{
	public IReadOnlyList<UpdateAdmissionCrmApplicationStagePolicyItemDto> Stages { get; set; } = [];
	public string? ExpectedImpactFingerprint { get; set; }
}

public class UpdateAdmissionCrmApplicationStagePolicyItemDto
{
	public AdmissionCrmApplicationStageKey StageKey { get; set; }
	public int DisplayOrder { get; set; }
	public bool IsEnabled { get; set; }
}

public class AdmissionCrmApplicationStagePolicyImpactDto
{
	public int ApplicantCount { get; set; }
	public string ImpactFingerprint { get; set; } = string.Empty;
	public bool RequiresConfirmation { get; set; }
	public IReadOnlyList<string> Warnings { get; set; } = [];
}

public class StartAdmissionCrmSupportSessionDto
{
	public Guid TenantId { get; set; }
	[Required, MaxLength(500)] public string Reason { get; set; } = string.Empty;
	[MaxLength(100)] public string? Reference { get; set; }
}

public class AdmissionCrmSupportSessionDto : EntityDto<Guid>
{
	public Guid TargetTenantId { get; set; }
	public string Reason { get; set; } = string.Empty;
	public string? Reference { get; set; }
	public DateTime ExpiresAt { get; set; }
	public DateTime? EndedAt { get; set; }
	public bool IsActive { get; set; }
}

public class CreateAdmissionCrmPlatformPresetDto
{
	[Required, MaxLength(64)] public string Code { get; set; } = string.Empty;
	[Required, MaxLength(160)] public string Name { get; set; } = string.Empty;
	public int Version { get; set; }
	public UpdateAdmissionCrmCycleConfigurationDto Configuration { get; set; } = new() { CycleName = string.Empty };
	public IReadOnlyList<UpdateAdmissionCrmSettingsAccessDto> AccessPolicy { get; set; } = [];
	[MaxLength(2000)] public string? ReleaseNotes { get; set; }
}

public class AdmissionCrmPlatformPresetDto : EntityDto<Guid>
{
	public string Code { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public int Version { get; set; }
	public AdmissionCrmPlatformPresetStatus Status { get; set; }
	public string? ReleaseNotes { get; set; }
	public DateTime? PublishedAt { get; set; }
}

public class AdmissionCrmPresetPreviewDto
{
	public Guid PresetId { get; set; }
	public int PresetVersion { get; set; }
	public bool HasChanges { get; set; }
	public IReadOnlyList<string> ChangedFields { get; set; } = [];
	public IReadOnlyList<string> ValidationErrors { get; set; } = [];
}

public class AdmissionCrmPresetOperationDto
{
	public Guid TenantId { get; set; }
	public Guid AcademicYearId { get; set; }
	public Guid PresetId { get; set; }
	public Guid SupportSessionId { get; set; }
	public string? ExpectedSnapshotHash { get; set; }
}

public class AdmissionCrmPresetApplyResultDto
{
	public Guid AdoptionId { get; set; }
	public string SnapshotHash { get; set; } = string.Empty;
	public DateTime AppliedAt { get; set; }
}

public interface IAdmissionCrmAdmissionYearSetupService : IApplicationService
{
	Task<IReadOnlyList<AdmissionCrmAdmissionYearSetupDto>> GetAdmissionYearVersionsAsync(
		Guid academicYearId);
	Task<IReadOnlyList<AdmissionCrmAdmissionYearStandardOptionDto>>
		GetAdmissionYearStandardOptionsAsync(Guid academicYearId);
	Task<AdmissionCrmAdmissionYearSetupDto> CreateAdmissionYearDraftAsync(Guid academicYearId);
	Task<AdmissionCrmAdmissionYearSetupDto> UpdateAdmissionYearDraftAsync(
		Guid id,
		UpdateAdmissionCrmAdmissionYearSetupDto input);
	Task<AdmissionCrmAdmissionYearSetupDto> PublishAdmissionYearAsync(
		Guid id,
		PublishAdmissionCrmAdmissionYearSetupDto input);
	Task<AdmissionCrmAdmissionYearSetupDto> CloseAdmissionYearAsync(
		Guid id,
		CloseAdmissionCrmAdmissionYearSetupDto input);
}

public interface IAdmissionCrmCycleReadinessService : IApplicationService
{
	Task<AdmissionCrmSetupStatusDto> GetStatusAsync(Guid academicYearId);
	Task<PagedResultDto<AdmissionCrmCycleListItemDto>> GetCyclesAsync(PagedResultRequestDto input);
	Task<AdmissionCrmCycleSummaryDto> GetCycleSummaryAsync(Guid academicYearId);
}

public interface IAdmissionCrmApplicationStagePolicyService : IApplicationService
{
	Task<AdmissionCrmApplicationStagePolicyDto> GetApplicationStagePolicyAsync(Guid academicYearId);
	Task<AdmissionCrmApplicationStagePolicyImpactDto> PreviewApplicationStagePolicyAsync(Guid academicYearId, UpdateAdmissionCrmApplicationStagePolicyDto input);
	Task<AdmissionCrmApplicationStagePolicyDto> UpdateApplicationStagePolicyAsync(Guid academicYearId, UpdateAdmissionCrmApplicationStagePolicyDto input);
}

public interface IAdmissionCrmSettingsAccessService : IApplicationService
{
	Task<IReadOnlyList<AdmissionCrmSettingsAccessDto>> GetAccessPolicyAsync();
	Task<IReadOnlyList<AdmissionCrmSettingsAccessDto>> UpdateAccessPolicyAsync(Guid tenantId, Guid supportSessionId, IReadOnlyList<UpdateAdmissionCrmSettingsAccessDto> input);
}

public interface IAdmissionCrmSupportSessionService : IApplicationService
{
	Task<AdmissionCrmSupportSessionDto> StartSupportSessionAsync(StartAdmissionCrmSupportSessionDto input);
	Task<AdmissionCrmSupportSessionDto> GetSupportSessionAsync(Guid id);
	Task EndSupportSessionAsync(Guid id);
}

public interface IAdmissionCrmPlatformPresetService : IApplicationService
{
	Task<AdmissionCrmPlatformPresetDto> CreatePresetAsync(CreateAdmissionCrmPlatformPresetDto input);
	Task<AdmissionCrmPlatformPresetDto> PublishPresetAsync(Guid id);
	Task<PagedResultDto<AdmissionCrmPlatformPresetDto>> GetPresetsAsync(PagedResultRequestDto input);
	Task<AdmissionCrmPresetPreviewDto> PreviewPresetAsync(AdmissionCrmPresetOperationDto input);
	Task<AdmissionCrmPresetApplyResultDto> ApplyPresetAsync(AdmissionCrmPresetOperationDto input);
	Task RollbackPresetAsync(Guid adoptionId, Guid supportSessionId);
}
