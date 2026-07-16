using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Users;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[Authorize]
[RemoteService(false)]
public class AdmissionCrmSetupAppService : ApplicationService, IAdmissionCrmSetupAppService
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
	private static readonly string[] SettingsKeys = ["journey", "inquiry-capture", "entrance-exams", "registration-documents", "offers-seats", "routing-scoring", "activation", "governance", "communications", "automation"];
	private readonly IRepository<AdmissionCrmSettingsAccessPolicy, Guid> _accessRepository;
	private readonly IRepository<AdmissionCrmPlatformSupportSession, Guid> _sessionRepository;
	private readonly IRepository<AdmissionCrmPlatformPreset, Guid> _presetRepository;
	private readonly IRepository<AdmissionCrmPresetAdoption, Guid> _adoptionRepository;
	private readonly IAdmissionCrmConfigurationAppService _configurationService;

	public AdmissionCrmSetupAppService(
		IRepository<AdmissionCrmSettingsAccessPolicy, Guid> accessRepository,
		IRepository<AdmissionCrmPlatformSupportSession, Guid> sessionRepository,
		IRepository<AdmissionCrmPlatformPreset, Guid> presetRepository,
		IRepository<AdmissionCrmPresetAdoption, Guid> adoptionRepository,
		IAdmissionCrmConfigurationAppService configurationService)
	{
		_accessRepository = accessRepository;
		_sessionRepository = sessionRepository;
		_presetRepository = presetRepository;
		_adoptionRepository = adoptionRepository;
		_configurationService = configurationService;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<AdmissionCrmSetupStatusDto> GetStatusAsync(Guid academicYearId)
	{
		var configuration = await _configurationService.GetCycleConfigurationAsync(academicYearId);
		var policies = await GetAccessPolicyAsync();
		var adoption = (await _adoptionRepository.GetListAsync(x => x.AcademicYearId == academicYearId && x.RolledBackAt == null))
			.OrderByDescending(x => x.AppliedAt)
			.FirstOrDefault();
		AdmissionCrmPlatformPreset? preset = null;
		if (adoption is not null)
		{
			using (CurrentTenant.Change(null))
			{
				preset = await _presetRepository.FindAsync(adoption.PresetId);
			}
		}

		var policyByKey = policies.ToDictionary(x => x.SettingsKey, StringComparer.OrdinalIgnoreCase);
		var coreReady = !string.IsNullOrWhiteSpace(configuration.CycleName) && configuration.DefaultLeadSlaHours > 0;
		var overall = adoption is not null ? AdmissionCrmSetupReadiness.Managed : coreReady ? AdmissionCrmSetupReadiness.Ready : AdmissionCrmSetupReadiness.NeedsAttention;
		var definitions = new[]
		{
			("journey", "Admission cycle and journey", "/student/enrollment/settings/journey"),
			("inquiry-capture", "Inquiry forms and sources", "/student/enrollment/settings/inquiry-capture"),
			("entrance-exams", "Exam and eligibility", "/student/enrollment/settings/entrance-exams"),
			("registration-documents", "Registration and documents", "/student/enrollment/settings/registration-documents"),
			("offers-seats", "Offers and seats", "/student/enrollment/settings/offers-seats"),
			("routing-scoring", "Assignment and priority", "/student/enrollment/settings/routing-scoring"),
			("activation", "Student activation", "/student/enrollment/settings/activation"),
			("governance", "Policies and compliance", "/student/enrollment/settings/governance"),
			("communications", "Messages and delivery", "/student/enrollment/settings/communications"),
			("automation", "Follow-up automation", "/student/enrollment/settings/automation")
		};

		return new AdmissionCrmSetupStatusDto
		{
			AcademicYearId = academicYearId,
			CycleName = configuration.CycleName,
			Status = overall,
			AppliedPresetName = preset?.Name,
			AppliedPresetVersion = adoption?.PresetVersion,
			LastUpdatedAt = adoption?.AppliedAt,
			Items = definitions.Select((item, index) =>
			{
				var mode = policyByKey.GetValueOrDefault(item.Item1)?.AccessMode ?? AdmissionCrmSettingsAccessMode.Editable;
				var status = mode == AdmissionCrmSettingsAccessMode.Hidden ? AdmissionCrmSetupReadiness.Locked : adoption is not null ? AdmissionCrmSetupReadiness.Managed : coreReady && index < 2 ? AdmissionCrmSetupReadiness.Ready : AdmissionCrmSetupReadiness.NeedsAttention;
				return new AdmissionCrmSetupItemDto { Key = item.Item1, Title = item.Item2, Path = item.Item3, AccessMode = mode, Status = status, BlockerCodes = status == AdmissionCrmSetupReadiness.NeedsAttention ? ["EnrollmentSetup:ReviewRequired"] : [] };
			}).ToList()
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<IReadOnlyList<AdmissionCrmSettingsAccessDto>> GetAccessPolicyAsync()
	{
		var configured = await _accessRepository.GetListAsync();
		return SettingsKeys.Select(key =>
		{
			var policy = configured.FirstOrDefault(x => x.SettingsKey == key && x.SectionKey == null);
			return new AdmissionCrmSettingsAccessDto { SettingsKey = key, AccessMode = policy?.AccessMode ?? AdmissionCrmSettingsAccessMode.Editable };
		}).ToList();
	}

	[Authorize(AdmissionCrmPlatformPermissions.StartSupportSession)]
	public async Task<AdmissionCrmSupportSessionDto> StartSupportSessionAsync(StartAdmissionCrmSupportSessionDto input)
	{
		EnsureHostContext();
		var now = Clock.Now;
		var session = new AdmissionCrmPlatformSupportSession(GuidGenerator.Create(), CurrentUser.GetId(), input.TenantId, input.Reason, input.Reference, now.AddMinutes(30));
		await _sessionRepository.InsertAsync(session, autoSave: true);
		return ToDto(session, now);
	}

	[Authorize(AdmissionCrmPlatformPermissions.StartSupportSession)]
	public async Task<AdmissionCrmSupportSessionDto> GetSupportSessionAsync(Guid id)
	{
		EnsureHostContext();
		return ToDto(await GetOwnedSessionAsync(id, null), Clock.Now);
	}

	[Authorize(AdmissionCrmPlatformPermissions.StartSupportSession)]
	public async Task EndSupportSessionAsync(Guid id)
	{
		EnsureHostContext();
		var session = await GetOwnedSessionAsync(id, null);
		session.End(Clock.Now);
		await _sessionRepository.UpdateAsync(session, autoSave: true);
	}

	[Authorize(AdmissionCrmPlatformPermissions.ManagePresets)]
	public async Task<AdmissionCrmPlatformPresetDto> CreatePresetAsync(CreateAdmissionCrmPlatformPresetDto input)
	{
		EnsureHostContext();
		var configurationJson = JsonSerializer.Serialize(input.Configuration, JsonOptions);
		var accessJson = JsonSerializer.Serialize(input.AccessPolicy, JsonOptions);
		var preset = new AdmissionCrmPlatformPreset(GuidGenerator.Create(), input.Code, input.Name, input.Version, configurationJson, accessJson, input.ReleaseNotes);
		await _presetRepository.InsertAsync(preset, autoSave: true);
		return ToDto(preset);
	}

	[Authorize(AdmissionCrmPlatformPermissions.ManagePresets)]
	public async Task<AdmissionCrmPlatformPresetDto> PublishPresetAsync(Guid id)
	{
		EnsureHostContext();
		var preset = await _presetRepository.GetAsync(id);
		preset.Publish(Clock.Now);
		await _presetRepository.UpdateAsync(preset, autoSave: true);
		return ToDto(preset);
	}

	[Authorize(AdmissionCrmPlatformPermissions.ManagePresets)]
	public async Task<PagedResultDto<AdmissionCrmPlatformPresetDto>> GetPresetsAsync(PagedResultRequestDto input)
	{
		EnsureHostContext();
		var query = await _presetRepository.GetQueryableAsync();
		var total = await AsyncExecuter.CountAsync(query);
		var items = await AsyncExecuter.ToListAsync(query.OrderBy(x => x.Code).ThenByDescending(x => x.Version).Skip(input.SkipCount).Take(input.MaxResultCount));
		return new PagedResultDto<AdmissionCrmPlatformPresetDto>(total, items.Select(ToDto).ToList());
	}

	[Authorize(AdmissionCrmPlatformPermissions.ApplyPresets)]
	public async Task<AdmissionCrmPresetPreviewDto> PreviewPresetAsync(AdmissionCrmPresetOperationDto input)
	{
		EnsureHostContext();
		await GetOwnedSessionAsync(input.SupportSessionId, input.TenantId);
		var preset = await GetPublishedPresetAsync(input.PresetId);
		AdmissionCrmCycleConfigurationDto current;
		using (CurrentTenant.Change(input.TenantId)) current = await _configurationService.GetCycleConfigurationAsync(input.AcademicYearId);
		var currentJson = JsonSerializer.Serialize(current, JsonOptions);
		var currentFields = JsonDocument.Parse(currentJson).RootElement.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.GetRawText());
		var presetFields = JsonDocument.Parse(preset.ConfigurationJson).RootElement.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.GetRawText());
		var changed = presetFields.Where(x => !currentFields.TryGetValue(x.Key, out var value) || value != x.Value).Select(x => x.Key).ToList();
		return new AdmissionCrmPresetPreviewDto { PresetId = preset.Id, PresetVersion = preset.Version, HasChanges = changed.Count > 0, ChangedFields = changed };
	}

	[Authorize(AdmissionCrmPlatformPermissions.ApplyPresets)]
	public async Task<AdmissionCrmPresetApplyResultDto> ApplyPresetAsync(AdmissionCrmPresetOperationDto input)
	{
		EnsureHostContext();
		await GetOwnedSessionAsync(input.SupportSessionId, input.TenantId);
		var preset = await GetPublishedPresetAsync(input.PresetId);
		AdmissionCrmCycleConfigurationDto current;
		using (CurrentTenant.Change(input.TenantId)) current = await _configurationService.GetCycleConfigurationAsync(input.AcademicYearId);
		var previousJson = JsonSerializer.Serialize(current, JsonOptions);
		var currentHash = Hash(previousJson);
		if (!string.IsNullOrWhiteSpace(input.ExpectedSnapshotHash) && !string.Equals(input.ExpectedSnapshotHash, currentHash, StringComparison.Ordinal)) throw new BusinessException("AdmissionCrm:PresetApplyConcurrencyConflict");
		var configuration = JsonSerializer.Deserialize<UpdateAdmissionCrmCycleConfigurationDto>(preset.ConfigurationJson, JsonOptions) ?? throw new BusinessException("AdmissionCrm:InvalidPlatformPresetConfiguration");
		using (CurrentTenant.Change(input.TenantId)) await _configurationService.UpdateCycleConfigurationAsync(input.AcademicYearId, configuration);
		var adoption = new AdmissionCrmPresetAdoption(GuidGenerator.Create(), input.TenantId, input.AcademicYearId, preset.Id, preset.Version, Hash(preset.ConfigurationJson), previousJson, Clock.Now);
		using (CurrentTenant.Change(input.TenantId)) await _adoptionRepository.InsertAsync(adoption, autoSave: true);
		await UpdateAccessPolicyCoreAsync(input.TenantId, JsonSerializer.Deserialize<List<UpdateAdmissionCrmSettingsAccessDto>>(preset.AccessPolicyJson, JsonOptions) ?? []);
		return new AdmissionCrmPresetApplyResultDto { AdoptionId = adoption.Id, SnapshotHash = adoption.SnapshotHash, AppliedAt = adoption.AppliedAt };
	}

	[Authorize(AdmissionCrmPlatformPermissions.ApplyPresets)]
	public async Task RollbackPresetAsync(Guid adoptionId, Guid supportSessionId)
	{
		EnsureHostContext();
		AdmissionCrmPresetAdoption adoption;
		using (DataFilter.Disable<Volo.Abp.MultiTenancy.IMultiTenant>()) adoption = await _adoptionRepository.GetAsync(adoptionId);
		await GetOwnedSessionAsync(supportSessionId, adoption.TenantId!.Value);
		if (adoption.RolledBackAt is not null) throw new BusinessException("AdmissionCrm:PresetAlreadyRolledBack");
		var previous = JsonSerializer.Deserialize<UpdateAdmissionCrmCycleConfigurationDto>(adoption.PreviousConfigurationJson, JsonOptions) ?? throw new BusinessException("AdmissionCrm:InvalidRollbackSnapshot");
		using (CurrentTenant.Change(adoption.TenantId))
		{
			await _configurationService.UpdateCycleConfigurationAsync(adoption.AcademicYearId, previous);
			adoption.MarkRolledBack(Clock.Now);
			await _adoptionRepository.UpdateAsync(adoption, autoSave: true);
		}
	}

	[Authorize(AdmissionCrmPlatformPermissions.ManageTenantAccess)]
	public async Task<IReadOnlyList<AdmissionCrmSettingsAccessDto>> UpdateAccessPolicyAsync(Guid tenantId, Guid supportSessionId, IReadOnlyList<UpdateAdmissionCrmSettingsAccessDto> input)
	{
		EnsureHostContext();
		await GetOwnedSessionAsync(supportSessionId, tenantId);
		return await UpdateAccessPolicyCoreAsync(tenantId, input);
	}

	private async Task<IReadOnlyList<AdmissionCrmSettingsAccessDto>> UpdateAccessPolicyCoreAsync(Guid tenantId, IReadOnlyList<UpdateAdmissionCrmSettingsAccessDto> input)
	{
		if (input.Any(x => !SettingsKeys.Contains(x.SettingsKey, StringComparer.OrdinalIgnoreCase))) throw new BusinessException("AdmissionCrm:UnknownSettingsKey");
		using (CurrentTenant.Change(tenantId))
		{
			var existing = await _accessRepository.GetListAsync();
			foreach (var item in input)
			{
				var policy = existing.FirstOrDefault(x => x.SettingsKey == item.SettingsKey && x.SectionKey == item.SectionKey);
				if (policy is null) await _accessRepository.InsertAsync(new AdmissionCrmSettingsAccessPolicy(GuidGenerator.Create(), tenantId, item.SettingsKey, item.SectionKey, item.AccessMode));
				else { policy.Update(item.SettingsKey, item.SectionKey, item.AccessMode); await _accessRepository.UpdateAsync(policy); }
			}
			await CurrentUnitOfWork!.SaveChangesAsync();
			return (await _accessRepository.GetListAsync()).Select(x => new AdmissionCrmSettingsAccessDto { SettingsKey = x.SettingsKey, SectionKey = x.SectionKey, AccessMode = x.AccessMode }).ToList();
		}
	}

	private async Task<AdmissionCrmPlatformSupportSession> GetOwnedSessionAsync(Guid id, Guid? targetTenantId)
	{
		var session = await _sessionRepository.GetAsync(id);
		if (session.OperatorUserId != CurrentUser.GetId() || (targetTenantId.HasValue && session.TargetTenantId != targetTenantId.Value) || !session.IsActive(Clock.Now)) throw new BusinessException("AdmissionCrm:InvalidOrExpiredSupportSession");
		return session;
	}

	private async Task<AdmissionCrmPlatformPreset> GetPublishedPresetAsync(Guid id)
	{
		var preset = await _presetRepository.GetAsync(id);
		if (preset.Status != AdmissionCrmPlatformPresetStatus.Published) throw new BusinessException("AdmissionCrm:PlatformPresetNotPublished");
		return preset;
	}

	private void EnsureHostContext()
	{
		if (CurrentTenant.Id is not null) throw new BusinessException("AdmissionCrm:PlatformOperatorRequiresHostContext");
	}

	private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
	private static AdmissionCrmSupportSessionDto ToDto(AdmissionCrmPlatformSupportSession session, DateTime now) => new() { Id = session.Id, TargetTenantId = session.TargetTenantId, Reason = session.Reason, Reference = session.Reference, ExpiresAt = session.ExpiresAt, EndedAt = session.EndedAt, IsActive = session.IsActive(now) };
	private static AdmissionCrmPlatformPresetDto ToDto(AdmissionCrmPlatformPreset preset) => new() { Id = preset.Id, Code = preset.Code, Name = preset.Name, Version = preset.Version, Status = preset.Status, ReleaseNotes = preset.ReleaseNotes, PublishedAt = preset.PublishedAt };
}
