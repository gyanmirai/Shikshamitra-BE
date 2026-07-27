using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicYears;
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
	private readonly IRepository<AcademicYear, Guid> _academicYearRepository;
	private readonly IRepository<AdmissionCrmCycleConfiguration, Guid> _cycleRepository;
	private readonly IRepository<AdmissionInquiryLead, Guid> _leadRepository;
	private readonly StudentScopeAccessService _scopeAccess;
	private readonly IAdmissionCrmConfigurationAppService _configurationService;
	private readonly AdmissionCrmConfigurationSafetyAppService _configurationSafety;

	public AdmissionCrmSetupAppService(
		IRepository<AdmissionCrmSettingsAccessPolicy, Guid> accessRepository,
		IRepository<AdmissionCrmPlatformSupportSession, Guid> sessionRepository,
		IRepository<AdmissionCrmPlatformPreset, Guid> presetRepository,
		IRepository<AdmissionCrmPresetAdoption, Guid> adoptionRepository,
		IRepository<AcademicYear, Guid> academicYearRepository,
		IRepository<AdmissionCrmCycleConfiguration, Guid> cycleRepository,
		IRepository<AdmissionInquiryLead, Guid> leadRepository,
		StudentScopeAccessService scopeAccess,
		IAdmissionCrmConfigurationAppService configurationService,
		AdmissionCrmConfigurationSafetyAppService configurationSafety)
	{
		_accessRepository = accessRepository;
		_sessionRepository = sessionRepository;
		_presetRepository = presetRepository;
		_adoptionRepository = adoptionRepository;
		_academicYearRepository = academicYearRepository;
		_cycleRepository = cycleRepository;
		_leadRepository = leadRepository;
		_scopeAccess = scopeAccess;
		_configurationService = configurationService;
		_configurationSafety = configurationSafety;
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
			("offers-seats", "Class Placement Plan", "/student/enrollment/settings/offers-seats"),
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
	public async Task<PagedResultDto<AdmissionCrmCycleListItemDto>> GetCyclesAsync(PagedResultRequestDto input)
	{
		var yearQuery = (await _academicYearRepository.GetQueryableAsync())
			.Where(year => year.TenantId == CurrentTenant.Id)
			.OrderByDescending(year => year.StartDate);
		var total = await AsyncExecuter.CountAsync(yearQuery);
		var years = await AsyncExecuter.ToListAsync(yearQuery
			.Skip(input.SkipCount)
			.Take(input.MaxResultCount));
		foreach (var year in years)
		{
			await _scopeAccess.EnsureAcademicYearWideConfigurationAccessAsync(year.Id, nameof(GetCyclesAsync));
		}

		var yearIds = years.Select(year => year.Id).ToList();
		var configurations = yearIds.Count == 0
			? []
			: await AsyncExecuter.ToListAsync((await _cycleRepository.GetQueryableAsync())
				.Where(configuration =>
					configuration.TenantId == CurrentTenant.Id &&
					yearIds.Contains(configuration.AcademicYearId)));
		var applicantCounts = await GetApplicantCountsAsync(yearIds);
		var rows = years
			.Select(year => BuildCycleListItem(
				year,
				configurations.FirstOrDefault(configuration => configuration.AcademicYearId == year.Id),
				applicantCounts.GetValueOrDefault(year.Id),
				null))
			.ToList();
		return new PagedResultDto<AdmissionCrmCycleListItemDto>(total, rows);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<AdmissionCrmCycleSummaryDto> GetCycleSummaryAsync(Guid academicYearId)
	{
		await _scopeAccess.EnsureAcademicYearWideConfigurationAccessAsync(
			academicYearId,
			nameof(GetCycleSummaryAsync));
		var year = await GetAcademicYearAsync(academicYearId);
		var readiness = await GetStatusAsync(academicYearId);
		var configuration = await _configurationService.GetCycleConfigurationAsync(academicYearId);
		var applicantCounts = await GetApplicantCountsAsync([academicYearId]);
		var cycle = BuildCycleListItem(
			year,
			await _cycleRepository.FindAsync(configuration.Id),
			applicantCounts.GetValueOrDefault(academicYearId),
			readiness);

		return new AdmissionCrmCycleSummaryDto
		{
			Cycle = cycle,
			Readiness = readiness,
			ApplicationStagePolicy = BuildApplicationStagePolicy(
				await _cycleRepository.GetAsync(configuration.Id),
				applicantCounts.GetValueOrDefault(academicYearId)),
			Policy = new AdmissionCrmCyclePolicySummaryDto
			{
				IsActive = configuration.IsActive,
				RegistrationFormRequiredBeforeExam = configuration.RegistrationFormRequiredBeforeExam,
				EntranceExamRequired = configuration.EntranceExamRequired,
				JourneyLocked = configuration.JourneyLocked,
				DefaultLeadSlaHours = configuration.DefaultLeadSlaHours,
				StaleAfterDays = configuration.StaleAfterDays,
				ArchiveAfterDays = configuration.ArchiveAfterDays,
				DefaultExamPassingPercentage = configuration.DefaultExamPassingPercentage,
				MeritRankingMode = configuration.MeritRankingMode
			}
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<AdmissionCrmApplicationStagePolicyDto> GetApplicationStagePolicyAsync(Guid academicYearId)
	{
		await _scopeAccess.EnsureAcademicYearWideConfigurationAccessAsync(
			academicYearId,
			nameof(GetApplicationStagePolicyAsync));
		var configuration = await GetCycleConfigurationEntityAsync(academicYearId);
		var applicantCounts = await GetApplicantCountsAsync([academicYearId]);
		return BuildApplicationStagePolicy(configuration, applicantCounts.GetValueOrDefault(academicYearId));
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<AdmissionCrmApplicationStagePolicyImpactDto> PreviewApplicationStagePolicyAsync(
		Guid academicYearId,
		UpdateAdmissionCrmApplicationStagePolicyDto input)
	{
		await _scopeAccess.EnsureAcademicYearWideConfigurationAccessAsync(
			academicYearId,
			nameof(PreviewApplicationStagePolicyAsync));
		var configuration = await GetCycleConfigurationEntityAsync(academicYearId);
		var applicantCounts = await GetApplicantCountsAsync([academicYearId]);
		var applicantCount = applicantCounts.GetValueOrDefault(academicYearId);
		var candidate = NormalizeApplicationStagePolicy(configuration, input);
		return BuildApplicationStagePolicyImpact(configuration, candidate, applicantCount);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<AdmissionCrmApplicationStagePolicyDto> UpdateApplicationStagePolicyAsync(
		Guid academicYearId,
		UpdateAdmissionCrmApplicationStagePolicyDto input)
	{
		await _scopeAccess.EnsureAcademicYearWideConfigurationAccessAsync(
			academicYearId,
			nameof(UpdateApplicationStagePolicyAsync));
		var configuration = await GetCycleConfigurationEntityAsync(academicYearId);
		if (configuration.JourneyLocked)
		{
			throw new BusinessException("AdmissionCrm:JourneyLocked");
		}

		var applicantCounts = await GetApplicantCountsAsync([academicYearId]);
		var applicantCount = applicantCounts.GetValueOrDefault(academicYearId);
		var candidate = NormalizeApplicationStagePolicy(configuration, input);
		var impact = BuildApplicationStagePolicyImpact(configuration, candidate, applicantCount);
		if (impact.RequiresConfirmation &&
			!string.Equals(input.ExpectedImpactFingerprint, impact.ImpactFingerprint, StringComparison.Ordinal))
		{
			throw new BusinessException("AdmissionCrm:StagePolicyImpactPreviewRequired")
				.WithData("ExpectedImpactFingerprint", impact.ImpactFingerprint);
		}

		configuration.UpdateApplicationStagePolicy(SerializeApplicationStagePolicy(candidate));
		await _cycleRepository.UpdateAsync(configuration, autoSave: true);
		return BuildApplicationStagePolicy(configuration, applicantCount);
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
		using (CurrentTenant.Change(input.TenantId))
		{
			configuration.ImpactPreviewFingerprint =
				await _configurationSafety.CreateCycleImpactFingerprintAsync(
					input.AcademicYearId,
					configuration,
					nameof(ApplyPresetAsync));
			await _configurationService.UpdateCycleConfigurationAsync(input.AcademicYearId, configuration);
		}
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
			previous.ImpactPreviewFingerprint =
				await _configurationSafety.CreateCycleImpactFingerprintAsync(
					adoption.AcademicYearId,
					previous,
					nameof(RollbackPresetAsync));
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

	private async Task<AcademicYear> GetAcademicYearAsync(Guid academicYearId)
	{
		var year = await _academicYearRepository.FindAsync(academicYearId);
		if (year is null || year.TenantId != CurrentTenant.Id)
		{
			throw new BusinessException("AdmissionCrm:AcademicYearNotFound")
				.WithData("AcademicYearId", academicYearId);
		}

		return year;
	}

	private async Task<Dictionary<Guid, int>> GetApplicantCountsAsync(IReadOnlyList<Guid> academicYearIds)
	{
		if (academicYearIds.Count == 0)
		{
			return [];
		}

		var leadQuery = (await _leadRepository.GetQueryableAsync())
			.Where(lead =>
				lead.TenantId == CurrentTenant.Id &&
				academicYearIds.Contains(lead.AcademicYearId));
		var counts = await AsyncExecuter.ToListAsync(
			leadQuery.GroupBy(lead => lead.AcademicYearId)
				.Select(group => new { AcademicYearId = group.Key, Count = group.Count() }));
		return counts.ToDictionary(item => item.AcademicYearId, item => item.Count);
	}

	private static AdmissionCrmCycleListItemDto BuildCycleListItem(
		AcademicYear year,
		AdmissionCrmCycleConfiguration? configuration,
		int applicantCount,
		AdmissionCrmSetupStatusDto? readiness)
	{
		var readinessStatus = readiness?.Status ?? GetReadiness(configuration);
		return new AdmissionCrmCycleListItemDto
		{
			AcademicYearId = year.Id,
			CycleConfigurationId = configuration?.Id,
			CycleName = configuration?.CycleName ?? $"{year.Name} Admission Cycle",
			AcademicYearName = year.Name,
			AcademicYearStartDate = year.StartDate,
			AcademicYearEndDate = year.EndDate,
			AdmissionWindowStart = null,
			AdmissionWindowEnd = null,
			Status = GetCycleStatus(configuration, readinessStatus),
			Readiness = readinessStatus,
			ApplicantCount = applicantCount,
			OwnerUserId = null,
			OwnerDisplayName = null,
			LastUpdatedAt = configuration?.LastModificationTime ?? configuration?.CreationTime,
			RequiredActions = readiness?.Items
				.Where(item => item.AccessMode != AdmissionCrmSettingsAccessMode.Hidden &&
					item.Status == AdmissionCrmSetupReadiness.NeedsAttention)
				.Select(item => new AdmissionCrmCycleRequiredActionDto
				{
					Key = item.Key,
					Title = item.Title,
					Path = item.Path,
					BlockerCodes = item.BlockerCodes
				})
				.ToList() ?? []
		};
	}

	private static AdmissionCrmSetupReadiness GetReadiness(AdmissionCrmCycleConfiguration? configuration)
	{
		if (configuration is null)
		{
			return AdmissionCrmSetupReadiness.NotStarted;
		}

		return !string.IsNullOrWhiteSpace(configuration.CycleName) && configuration.DefaultLeadSlaHours > 0
			? AdmissionCrmSetupReadiness.Ready
			: AdmissionCrmSetupReadiness.NeedsAttention;
	}

	private static AdmissionCrmCycleStatus GetCycleStatus(
		AdmissionCrmCycleConfiguration? configuration,
		AdmissionCrmSetupReadiness readiness)
	{
		if (configuration is null)
		{
			return AdmissionCrmCycleStatus.Draft;
		}

		if (configuration.JourneyLocked)
		{
			return AdmissionCrmCycleStatus.Paused;
		}

		if (!configuration.IsActive)
		{
			return AdmissionCrmCycleStatus.Draft;
		}

		return readiness is AdmissionCrmSetupReadiness.Ready or AdmissionCrmSetupReadiness.Managed
			? AdmissionCrmCycleStatus.Open
			: AdmissionCrmCycleStatus.ReadinessReview;
	}

	private async Task<AdmissionCrmCycleConfiguration> GetCycleConfigurationEntityAsync(Guid academicYearId)
	{
		var configuration = await _configurationService.GetCycleConfigurationAsync(academicYearId);
		return await _cycleRepository.GetAsync(configuration.Id);
	}

	private static AdmissionCrmApplicationStagePolicyDto BuildApplicationStagePolicy(
		AdmissionCrmCycleConfiguration configuration,
		int applicantCount)
	{
		var policy = NormalizeStoredApplicationStagePolicy(configuration);
		policy.EntranceExamRequired = configuration.EntranceExamRequired;
		policy.JourneyLocked = configuration.JourneyLocked;
		policy.ApplicantCount = applicantCount;
		policy.ImpactFingerprint = BuildApplicationStagePolicyFingerprint(
			configuration,
			policy.Stages,
			applicantCount);
		return policy;
	}

	private static AdmissionCrmApplicationStagePolicyImpactDto BuildApplicationStagePolicyImpact(
		AdmissionCrmCycleConfiguration configuration,
		AdmissionCrmApplicationStagePolicyDto candidate,
		int applicantCount)
	{
		var current = NormalizeStoredApplicationStagePolicy(configuration);
		var warnings = new List<string>();
		if (applicantCount > 0)
		{
			warnings.Add("Existing applicants stay in their current CRM stage; this policy changes the cycle setup summary and future stage guidance only.");
		}

		if (current.Stages.Any(stage =>
			stage.IsExamStage &&
			stage.IsEnabled &&
			candidate.Stages.Any(next => next.StageKey == stage.StageKey && !next.IsEnabled)))
		{
			warnings.Add("Exam stages will be hidden from the configured journey because this cycle does not require an entrance exam.");
		}

		if (current.Stages.Any(stage =>
			candidate.Stages.Any(next =>
				next.StageKey == stage.StageKey &&
				next.DisplayOrder != stage.DisplayOrder)))
		{
			warnings.Add("Stage ordering changes how administrators read this cycle journey; it does not migrate applicant history.");
		}

		return new AdmissionCrmApplicationStagePolicyImpactDto
		{
			ApplicantCount = applicantCount,
			ImpactFingerprint = BuildApplicationStagePolicyFingerprint(
				configuration,
				candidate.Stages,
				applicantCount),
			RequiresConfirmation = applicantCount > 0 || configuration.IsActive,
			Warnings = warnings
		};
	}

	private static AdmissionCrmApplicationStagePolicyDto NormalizeStoredApplicationStagePolicy(
		AdmissionCrmCycleConfiguration configuration)
	{
		AdmissionCrmApplicationStagePolicyDto? stored = null;
		if (!string.IsNullOrWhiteSpace(configuration.ApplicationStagePolicyJson) &&
			configuration.ApplicationStagePolicyJson.Trim() != "{}")
		{
			stored = JsonSerializer.Deserialize<AdmissionCrmApplicationStagePolicyDto>(
				configuration.ApplicationStagePolicyJson,
				JsonOptions);
		}

		var requested = stored?.Stages.Select(stage => new UpdateAdmissionCrmApplicationStagePolicyItemDto
		{
			StageKey = stage.StageKey,
			DisplayOrder = stage.DisplayOrder,
			IsEnabled = stage.IsEnabled
		}).ToList() ?? BuildDefaultApplicationStageUpdates(configuration.EntranceExamRequired);
		return NormalizeApplicationStagePolicy(configuration, new UpdateAdmissionCrmApplicationStagePolicyDto { Stages = requested });
	}

	private static AdmissionCrmApplicationStagePolicyDto NormalizeApplicationStagePolicy(
		AdmissionCrmCycleConfiguration configuration,
		UpdateAdmissionCrmApplicationStagePolicyDto input)
	{
		var definitions = ApplicationStageDefinitions.ToDictionary(stage => stage.StageKey);
		if (input.Stages.Count != definitions.Count)
		{
			throw new BusinessException("AdmissionCrm:ApplicationStagePolicyIncomplete");
		}

		var duplicateStage = input.Stages.GroupBy(stage => stage.StageKey).FirstOrDefault(group => group.Count() > 1);
		if (duplicateStage is not null)
		{
			throw new BusinessException("AdmissionCrm:DuplicateApplicationStage")
				.WithData("StageKey", duplicateStage.Key);
		}

		var duplicateOrder = input.Stages.GroupBy(stage => stage.DisplayOrder).FirstOrDefault(group => group.Count() > 1);
		if (duplicateOrder is not null)
		{
			throw new BusinessException("AdmissionCrm:DuplicateApplicationStageOrder")
				.WithData("DisplayOrder", duplicateOrder.Key);
		}

		var items = new List<AdmissionCrmApplicationStagePolicyItemDto>();
		foreach (var requested in input.Stages.OrderBy(stage => stage.DisplayOrder))
		{
			if (!definitions.TryGetValue(requested.StageKey, out var definition))
			{
				throw new BusinessException("AdmissionCrm:UnsupportedApplicationStage")
					.WithData("StageKey", requested.StageKey);
			}

			if (requested.DisplayOrder <= 0)
			{
				throw new BusinessException("AdmissionCrm:InvalidApplicationStageOrder")
					.WithData("StageKey", requested.StageKey)
					.WithData("DisplayOrder", requested.DisplayOrder);
			}

			if (definition.IsRequired && !definition.IsExamStage && !requested.IsEnabled)
			{
				throw new BusinessException("AdmissionCrm:RequiredApplicationStageDisabled")
					.WithData("StageKey", requested.StageKey);
			}

			if (configuration.EntranceExamRequired && definition.IsExamStage && !requested.IsEnabled)
			{
				throw new BusinessException("AdmissionCrm:RequiredApplicationStageDisabled")
					.WithData("StageKey", requested.StageKey);
			}

			items.Add(new AdmissionCrmApplicationStagePolicyItemDto
			{
				StageKey = definition.StageKey,
				DisplayName = definition.DisplayName,
				DisplayOrder = requested.DisplayOrder,
				IsEnabled = definition.IsExamStage && !configuration.EntranceExamRequired ? false : requested.IsEnabled,
				IsRequired = definition.IsRequired || (configuration.EntranceExamRequired && definition.IsExamStage),
				IsSystemLocked = !definition.IsExamStage,
				IsExamStage = definition.IsExamStage,
				IsOperationallyBacked = definition.SupportedOperationalStages.Count > 0,
				SupportedOperationalStages = definition.SupportedOperationalStages,
				ReadinessNote = definition.ReadinessNote
			});
		}

		return new AdmissionCrmApplicationStagePolicyDto
		{
			Version = 1,
			EntranceExamRequired = configuration.EntranceExamRequired,
			JourneyLocked = configuration.JourneyLocked,
			Stages = items.OrderBy(stage => stage.DisplayOrder).ToList()
		};
	}

	private static List<UpdateAdmissionCrmApplicationStagePolicyItemDto> BuildDefaultApplicationStageUpdates(
		bool entranceExamRequired)
	{
		return ApplicationStageDefinitions.Select((definition, index) => new UpdateAdmissionCrmApplicationStagePolicyItemDto
		{
			StageKey = definition.StageKey,
			DisplayOrder = index + 1,
			IsEnabled = !definition.IsExamStage || entranceExamRequired
		}).ToList();
	}

	private static string SerializeApplicationStagePolicy(AdmissionCrmApplicationStagePolicyDto policy)
	{
		return JsonSerializer.Serialize(
			new AdmissionCrmApplicationStagePolicyDto
			{
				Version = policy.Version,
				EntranceExamRequired = policy.EntranceExamRequired,
				JourneyLocked = policy.JourneyLocked,
				Stages = policy.Stages
			},
			JsonOptions);
	}

	private static string BuildApplicationStagePolicyFingerprint(
		AdmissionCrmCycleConfiguration configuration,
		IReadOnlyList<AdmissionCrmApplicationStagePolicyItemDto> stages,
		int applicantCount)
	{
		var payload = JsonSerializer.Serialize(new
		{
			configuration.AcademicYearId,
			configuration.IsActive,
			configuration.JourneyLocked,
			configuration.EntranceExamRequired,
			applicantCount,
			stages = stages
				.OrderBy(stage => stage.DisplayOrder)
				.Select(stage => new { stage.StageKey, stage.DisplayOrder, stage.IsEnabled })
		}, JsonOptions);
		return Hash(payload);
	}

	private sealed record ApplicationStageDefinition(
		AdmissionCrmApplicationStageKey StageKey,
		string DisplayName,
		bool IsRequired,
		bool IsExamStage,
		IReadOnlyList<AdmissionInquiryStage> SupportedOperationalStages,
		string ReadinessNote);

	private static readonly IReadOnlyList<ApplicationStageDefinition> ApplicationStageDefinitions =
	[
		new(
			AdmissionCrmApplicationStageKey.Inquiry,
			"Inquiry",
			true,
			false,
			[AdmissionInquiryStage.NewInquiry],
			"Operationally backed by the current admissions CRM inquiry stage."),
		new(
			AdmissionCrmApplicationStageKey.QualificationFollowUp,
			"Qualification / Follow-up",
			true,
			false,
			[AdmissionInquiryStage.FollowUp, AdmissionInquiryStage.ExamReady],
			"Operationally backed by follow-up and exam-readiness stages."),
		new(
			AdmissionCrmApplicationStageKey.ExamScheduling,
			"Exam Scheduling",
			false,
			true,
			[AdmissionInquiryStage.ExamScheduled],
			"Operationally backed when the cycle requires an entrance exam."),
		new(
			AdmissionCrmApplicationStageKey.ExamResultEligibility,
			"Exam Result / Eligibility",
			false,
			true,
			[AdmissionInquiryStage.ExamCompleted, AdmissionInquiryStage.Eligible, AdmissionInquiryStage.Waitlisted, AdmissionInquiryStage.Rejected],
			"Operationally backed by exam result and eligibility CRM stages."),
		new(
			AdmissionCrmApplicationStageKey.RegistrationDataEntry,
			"Registration / Data Entry",
			true,
			false,
			[],
			"Policy-only in this phase; detailed registration and assisted data entry are later phases."),
		new(
			AdmissionCrmApplicationStageKey.DocumentsVerification,
			"Documents / Verification",
			true,
			false,
			[],
			"Policy-only in this phase; document checklist execution remains in existing settings."),
		new(
			AdmissionCrmApplicationStageKey.FeeVerification,
			"Fee Verification",
			true,
			false,
			[],
			"Policy-only in this phase; no Finance integration is introduced."),
		new(
			AdmissionCrmApplicationStageKey.ClassPlacement,
			"Class Placement",
			true,
			false,
			[],
			"Policy-only in this phase; Class Placement Plan integration is later."),
		new(
			AdmissionCrmApplicationStageKey.AdmissionConfirmation,
			"Admission Confirmation",
			true,
			false,
			[],
			"Policy-only in this phase; confirmation package workflow is later."),
		new(
			AdmissionCrmApplicationStageKey.Activation,
			"Activation",
			true,
			false,
			[],
			"Policy-only in this phase; student record and GR workflow are later."),
		new(
			AdmissionCrmApplicationStageKey.Closed,
			"Closed",
			true,
			false,
			[AdmissionInquiryStage.Archived, AdmissionInquiryStage.Stale, AdmissionInquiryStage.Rejected],
			"Operationally backed by terminal CRM stages.")
	];

	private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
	private static AdmissionCrmSupportSessionDto ToDto(AdmissionCrmPlatformSupportSession session, DateTime now) => new() { Id = session.Id, TargetTenantId = session.TargetTenantId, Reason = session.Reason, Reference = session.Reference, ExpiresAt = session.ExpiresAt, EndedAt = session.EndedAt, IsActive = session.IsActive(now) };
	private static AdmissionCrmPlatformPresetDto ToDto(AdmissionCrmPlatformPreset preset) => new() { Id = preset.Id, Code = preset.Code, Name = preset.Name, Version = preset.Version, Status = preset.Status, ReleaseNotes = preset.ReleaseNotes, PublishedAt = preset.PublishedAt };
}
