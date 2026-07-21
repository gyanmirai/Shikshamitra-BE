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

public interface IAdmissionCrmSetupAppService : IApplicationService
{
	Task<AdmissionCrmSetupStatusDto> GetStatusAsync(Guid academicYearId);
	Task<IReadOnlyList<AdmissionCrmSettingsAccessDto>> GetAccessPolicyAsync();
	Task<AdmissionCrmSupportSessionDto> StartSupportSessionAsync(StartAdmissionCrmSupportSessionDto input);
	Task<AdmissionCrmSupportSessionDto> GetSupportSessionAsync(Guid id);
	Task EndSupportSessionAsync(Guid id);
	Task<AdmissionCrmPlatformPresetDto> CreatePresetAsync(CreateAdmissionCrmPlatformPresetDto input);
	Task<AdmissionCrmPlatformPresetDto> PublishPresetAsync(Guid id);
	Task<PagedResultDto<AdmissionCrmPlatformPresetDto>> GetPresetsAsync(PagedResultRequestDto input);
	Task<AdmissionCrmPresetPreviewDto> PreviewPresetAsync(AdmissionCrmPresetOperationDto input);
	Task<AdmissionCrmPresetApplyResultDto> ApplyPresetAsync(AdmissionCrmPresetOperationDto input);
	Task RollbackPresetAsync(Guid adoptionId, Guid supportSessionId);
	Task<IReadOnlyList<AdmissionCrmSettingsAccessDto>> UpdateAccessPolicyAsync(Guid tenantId, Guid supportSessionId, IReadOnlyList<UpdateAdmissionCrmSettingsAccessDto> input);
}
