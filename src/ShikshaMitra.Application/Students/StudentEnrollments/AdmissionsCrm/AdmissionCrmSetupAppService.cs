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
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Users;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[Authorize]
[RemoteService(false)]
public class AdmissionCrmSetupAppService : ApplicationService, IAdmissionCrmSetupAppService
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
	private static readonly HashSet<string> AdmissionYearStepKeys =
	[
		"inquiry",
		"exam",
		"registration",
		"finance",
		"placement",
		"activation"
	];
	private static readonly string[] SettingsKeys = ["journey", "inquiry-capture", "entrance-exams", "registration-documents", "offers-seats", "routing-scoring", "activation", "governance", "communications", "automation"];
	private readonly IRepository<AdmissionCrmSettingsAccessPolicy, Guid> _accessRepository;
	private readonly IRepository<AdmissionCrmPlatformSupportSession, Guid> _sessionRepository;
	private readonly IRepository<AdmissionCrmPlatformPreset, Guid> _presetRepository;
	private readonly IRepository<AdmissionCrmPresetAdoption, Guid> _adoptionRepository;
	private readonly IRepository<AcademicYear, Guid> _academicYearRepository;
	private readonly IRepository<AdmissionCrmCycleConfiguration, Guid> _cycleRepository;
	private readonly IRepository<AdmissionCrmAdmissionYearSetup, Guid> _admissionYearRepository;
	private readonly IRepository<AdmissionCrmInquirySource, Guid> _inquirySourceRepository;
	private readonly IRepository<AdmissionCrmInquiryType, Guid> _inquiryTypeRepository;
	private readonly IRepository<AdmissionCrmPlaybook, Guid> _playbookRepository;
	private readonly IRepository<AdmissionCrmRegistrationTemplate, Guid> _registrationTemplateRepository;
	private readonly IRepository<AdmissionCrmDocumentChecklistTemplate, Guid> _documentTemplateRepository;
	private readonly IRepository<AdmissionCrmMessageTemplate, Guid> _messageTemplateRepository;
	private readonly IRepository<AdmissionInquiryLead, Guid> _leadRepository;
	private readonly IRepository<Standard, Guid> _standardRepository;
	private readonly IRepository<EducationMediumStage, Guid> _mediumStageRepository;
	private readonly IRepository<EducationMedium, Guid> _mediumRepository;
	private readonly IRepository<EducationBoard, Guid> _boardRepository;
	private readonly StudentScopeAccessService _scopeAccess;
	private readonly IAdmissionCrmConfigurationAppService _configurationService;
	private readonly AdmissionCrmConfigurationSafetyAppService _configurationSafety;
	private readonly AdmissionCrmStaffEligibilityService _staffEligibilityService;
	private readonly IAbpDistributedLock _distributedLock;

	public AdmissionCrmSetupAppService(
		IRepository<AdmissionCrmSettingsAccessPolicy, Guid> accessRepository,
		IRepository<AdmissionCrmPlatformSupportSession, Guid> sessionRepository,
		IRepository<AdmissionCrmPlatformPreset, Guid> presetRepository,
		IRepository<AdmissionCrmPresetAdoption, Guid> adoptionRepository,
		IRepository<AcademicYear, Guid> academicYearRepository,
		IRepository<AdmissionCrmCycleConfiguration, Guid> cycleRepository,
		IRepository<AdmissionCrmAdmissionYearSetup, Guid> admissionYearRepository,
		IRepository<AdmissionCrmInquirySource, Guid> inquirySourceRepository,
		IRepository<AdmissionCrmInquiryType, Guid> inquiryTypeRepository,
		IRepository<AdmissionCrmPlaybook, Guid> playbookRepository,
		IRepository<AdmissionCrmRegistrationTemplate, Guid> registrationTemplateRepository,
		IRepository<AdmissionCrmDocumentChecklistTemplate, Guid> documentTemplateRepository,
		IRepository<AdmissionCrmMessageTemplate, Guid> messageTemplateRepository,
		IRepository<AdmissionInquiryLead, Guid> leadRepository,
		IRepository<Standard, Guid> standardRepository,
		IRepository<EducationMediumStage, Guid> mediumStageRepository,
		IRepository<EducationMedium, Guid> mediumRepository,
		IRepository<EducationBoard, Guid> boardRepository,
		StudentScopeAccessService scopeAccess,
		IAdmissionCrmConfigurationAppService configurationService,
		AdmissionCrmConfigurationSafetyAppService configurationSafety,
		AdmissionCrmStaffEligibilityService staffEligibilityService,
		IAbpDistributedLock distributedLock)
	{
		_accessRepository = accessRepository;
		_sessionRepository = sessionRepository;
		_presetRepository = presetRepository;
		_adoptionRepository = adoptionRepository;
		_academicYearRepository = academicYearRepository;
		_cycleRepository = cycleRepository;
		_admissionYearRepository = admissionYearRepository;
		_inquirySourceRepository = inquirySourceRepository;
		_inquiryTypeRepository = inquiryTypeRepository;
		_playbookRepository = playbookRepository;
		_registrationTemplateRepository = registrationTemplateRepository;
		_documentTemplateRepository = documentTemplateRepository;
		_messageTemplateRepository = messageTemplateRepository;
		_leadRepository = leadRepository;
		_standardRepository = standardRepository;
		_mediumStageRepository = mediumStageRepository;
		_mediumRepository = mediumRepository;
		_boardRepository = boardRepository;
		_scopeAccess = scopeAccess;
		_configurationService = configurationService;
		_configurationSafety = configurationSafety;
		_staffEligibilityService = staffEligibilityService;
		_distributedLock = distributedLock;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<IReadOnlyList<AdmissionCrmAdmissionYearSetupDto>> GetAdmissionYearVersionsAsync(
		Guid academicYearId)
	{
		await _scopeAccess.EnsureAcademicYearWideConfigurationAccessAsync(
			academicYearId,
			nameof(GetAdmissionYearVersionsAsync));
		await GetAcademicYearAsync(academicYearId);
		var query = (await _admissionYearRepository.GetQueryableAsync())
			.Where(setup =>
				setup.TenantId == CurrentTenant.Id &&
				setup.AcademicYearId == academicYearId)
			.OrderByDescending(setup => setup.Version);
		return (await AsyncExecuter.ToListAsync(query))
			.Select(ToAdmissionYearDto)
			.ToList();
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<IReadOnlyList<AdmissionCrmAdmissionYearStandardOptionDto>>
		GetAdmissionYearStandardOptionsAsync(Guid academicYearId)
	{
		await _scopeAccess.EnsureAcademicYearWideConfigurationAccessAsync(
			academicYearId,
			nameof(GetAdmissionYearStandardOptionsAsync));
		await GetAcademicYearAsync(academicYearId);
		var rows = await GetAdmissionYearStandardRowsAsync(academicYearId);
		return rows
			.OrderBy(row => row.BoardType)
			.ThenBy(row => row.MediumName)
			.ThenBy(row => row.MediumStageName)
			.ThenBy(row => row.StandardName)
			.Select(row => new AdmissionCrmAdmissionYearStandardOptionDto
			{
				StandardId = row.StandardId,
				StandardName = row.StandardName,
				MediumStageId = row.MediumStageId,
				MediumStageName = row.MediumStageName,
				MediumId = row.MediumId,
				MediumName = row.MediumName,
				BoardId = row.BoardId,
				BoardName = row.BoardType.ToString()
			})
			.ToList();
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<AdmissionCrmAdmissionYearSetupDto> CreateAdmissionYearDraftAsync(
		Guid academicYearId)
	{
		await _scopeAccess.EnsureAcademicYearWideConfigurationAccessAsync(
			academicYearId,
			nameof(CreateAdmissionYearDraftAsync));
		await using var lockHandle = await _distributedLock.TryAcquireAsync(
			GetAdmissionYearLockKey(academicYearId),
			TimeSpan.FromSeconds(15));
		if (lockHandle is null)
		{
			throw new BusinessException("AdmissionCrm:AdmissionYearSetupBusy")
				.WithData("AcademicYearId", academicYearId);
		}

		var year = await GetAcademicYearAsync(academicYearId);
		var existing = await _admissionYearRepository.GetListAsync(setup =>
			setup.TenantId == CurrentTenant.Id &&
			setup.AcademicYearId == academicYearId);
		var currentDraft = existing.FirstOrDefault(setup =>
			setup.Status == AdmissionCrmAdmissionYearSetupStatus.Draft);
		if (currentDraft is not null)
		{
			return ToAdmissionYearDto(currentDraft);
		}

		var source = existing
			.OrderByDescending(setup => setup.Version)
			.FirstOrDefault();
		var setup = source is null
			? new AdmissionCrmAdmissionYearSetup(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				academicYearId,
				1,
				year.StartDate.Date.AddMonths(-6),
				year.StartDate.Date,
				year.StartDate.Date,
				year.StartDate.Date,
				true,
				false,
				AdmissionCrmPlacementTiming.StandardBeforeActivation,
				null,
				"[]",
				JsonSerializer.Serialize(new[] { AdmissionCrmRouteType.Fresh }, JsonOptions),
				"{}")
			: new AdmissionCrmAdmissionYearSetup(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				academicYearId,
				source.Version + 1,
				source.InquiryOpenDate,
				source.InquiryCloseDate,
				source.RegistrationCloseDate,
				source.ActivationTargetDate,
				source.EntranceExamRequired,
				source.FinanceVerificationRequired,
				source.PlacementTiming,
				source.DefaultOwnerUserId,
				source.TargetStandardIdsJson,
				source.RouteTypesJson,
				source.StepOwnersJson);

		await _admissionYearRepository.InsertAsync(setup, autoSave: true);
		return ToAdmissionYearDto(setup);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<AdmissionCrmAdmissionYearSetupDto> UpdateAdmissionYearDraftAsync(
		Guid id,
		UpdateAdmissionCrmAdmissionYearSetupDto input)
	{
		var setup = await GetAdmissionYearForCurrentTenantAsync(
			id,
			nameof(UpdateAdmissionYearDraftAsync));
		await _scopeAccess.EnsureAcademicYearWideConfigurationAccessAsync(
			setup.AcademicYearId,
			nameof(UpdateAdmissionYearDraftAsync));
		await using var lockHandle = await _distributedLock.TryAcquireAsync(
			GetAdmissionYearLockKey(setup.AcademicYearId),
			TimeSpan.FromSeconds(15));
		if (lockHandle is null)
		{
			throw new BusinessException("AdmissionCrm:AdmissionYearSetupBusy")
				.WithData("AcademicYearId", setup.AcademicYearId);
		}

		EnsureAdmissionYearConcurrency(setup, input.ConcurrencyStamp);

		await ValidateAdmissionYearInputAsync(setup.AcademicYearId, input);
		setup.Update(
			input.InquiryOpenDate,
			input.InquiryCloseDate,
			input.RegistrationCloseDate,
			input.ActivationTargetDate,
			input.EntranceExamRequired,
			input.FinanceVerificationRequired,
			input.PlacementTiming,
			input.DefaultOwnerUserId,
			JsonSerializer.Serialize(input.TargetStandardIds.Distinct(), JsonOptions),
			JsonSerializer.Serialize(input.RouteTypes.Distinct(), JsonOptions),
			JsonSerializer.Serialize(input.StepOwners, JsonOptions));
		await _admissionYearRepository.UpdateAsync(setup, autoSave: true);
		return ToAdmissionYearDto(setup);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<AdmissionCrmAdmissionYearSetupDto> PublishAdmissionYearAsync(
		Guid id,
		PublishAdmissionCrmAdmissionYearSetupDto input)
	{
		var setup = await GetAdmissionYearForCurrentTenantAsync(id, nameof(PublishAdmissionYearAsync));
		await _scopeAccess.EnsureAcademicYearWideConfigurationAccessAsync(
			setup.AcademicYearId,
			nameof(PublishAdmissionYearAsync));
		await using var lockHandle = await _distributedLock.TryAcquireAsync(
			GetAdmissionYearLockKey(setup.AcademicYearId),
			TimeSpan.FromSeconds(15));
		if (lockHandle is null)
		{
			throw new BusinessException("AdmissionCrm:AdmissionYearSetupBusy")
				.WithData("AcademicYearId", setup.AcademicYearId);
		}

		EnsureAdmissionYearConcurrency(setup, input.ConcurrencyStamp);
		var candidate = ToAdmissionYearDto(setup);
		await ValidateAdmissionYearInputAsync(
			setup.AcademicYearId,
			new UpdateAdmissionCrmAdmissionYearSetupDto
			{
				ConcurrencyStamp = candidate.ConcurrencyStamp,
				InquiryOpenDate = candidate.InquiryOpenDate,
				InquiryCloseDate = candidate.InquiryCloseDate,
				RegistrationCloseDate = candidate.RegistrationCloseDate,
				ActivationTargetDate = candidate.ActivationTargetDate,
				EntranceExamRequired = candidate.EntranceExamRequired,
				FinanceVerificationRequired = candidate.FinanceVerificationRequired,
				PlacementTiming = candidate.PlacementTiming,
				DefaultOwnerUserId = candidate.DefaultOwnerUserId,
				TargetStandardIds = candidate.TargetStandardIds,
				RouteTypes = candidate.RouteTypes,
				StepOwners = candidate.StepOwners
			},
			requirePublishReady: true);

		var published = await _admissionYearRepository.FirstOrDefaultAsync(existing =>
			existing.TenantId == CurrentTenant.Id &&
			existing.AcademicYearId == setup.AcademicYearId &&
			existing.Status == AdmissionCrmAdmissionYearSetupStatus.Published);
		if (published is not null && published.Id != setup.Id)
		{
			published.Supersede();
			await _admissionYearRepository.UpdateAsync(published, autoSave: true);
		}

		var snapshot = JsonSerializer.Serialize(candidate, JsonOptions);
		setup.Publish(snapshot, CurrentUser.GetId(), Clock.Now);
		await _admissionYearRepository.UpdateAsync(setup, autoSave: true);
		return ToAdmissionYearDto(setup);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<AdmissionCrmAdmissionYearSetupDto> CloseAdmissionYearAsync(
		Guid id,
		CloseAdmissionCrmAdmissionYearSetupDto input)
	{
		var setup = await GetAdmissionYearForCurrentTenantAsync(id, nameof(CloseAdmissionYearAsync));
		await _scopeAccess.EnsureAcademicYearWideConfigurationAccessAsync(
			setup.AcademicYearId,
			nameof(CloseAdmissionYearAsync));
		await using var lockHandle = await _distributedLock.TryAcquireAsync(
			GetAdmissionYearLockKey(setup.AcademicYearId),
			TimeSpan.FromSeconds(15));
		if (lockHandle is null)
		{
			throw new BusinessException("AdmissionCrm:AdmissionYearSetupBusy")
				.WithData("AcademicYearId", setup.AcademicYearId);
		}

		EnsureAdmissionYearConcurrency(setup, input.ConcurrencyStamp);
		setup.Close(input.Reason, CurrentUser.GetId(), Clock.Now);
		await _admissionYearRepository.UpdateAsync(setup, autoSave: true);
		return ToAdmissionYearDto(setup);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<AdmissionCrmSetupStatusDto> GetStatusAsync(Guid academicYearId)
	{
		await _scopeAccess.EnsureAcademicYearWideConfigurationAccessAsync(
			academicYearId,
			nameof(GetStatusAsync));
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

		var tenantId = CurrentTenant.Id;
		var admissionYearQuery = await _admissionYearRepository.GetQueryableAsync();
		var publishedAdmissionYear = await AsyncExecuter.FirstOrDefaultAsync(
			admissionYearQuery.Where(setup =>
				setup.TenantId == tenantId &&
				setup.AcademicYearId == academicYearId &&
				setup.Status == AdmissionCrmAdmissionYearSetupStatus.Published));
		var inquirySourceQuery = await _inquirySourceRepository.GetQueryableAsync();
		var hasActiveSource = await AsyncExecuter.AnyAsync(inquirySourceQuery.Where(source =>
			source.TenantId == tenantId &&
			source.AcademicYearId == academicYearId &&
			source.IsActive));
		var inquiryTypeQuery = await _inquiryTypeRepository.GetQueryableAsync();
		var hasActiveType = await AsyncExecuter.AnyAsync(inquiryTypeQuery.Where(type =>
			type.TenantId == tenantId &&
			type.AcademicYearId == academicYearId &&
			type.IsActive));
		var playbookQuery = await _playbookRepository.GetQueryableAsync();
		var hasPublishedExamPolicy = await AsyncExecuter.AnyAsync(playbookQuery.Where(playbook =>
			playbook.TenantId == tenantId &&
			playbook.AcademicYearId == academicYearId &&
			playbook.Status == AdmissionCrmPlaybookStatus.Published &&
			playbook.ExamDefinitionsJson != "[]"));
		var registrationQuery = await _registrationTemplateRepository.GetQueryableAsync();
		var hasPublishedRegistration = await AsyncExecuter.AnyAsync(registrationQuery.Where(template =>
			template.TenantId == tenantId &&
			template.AcademicYearId == academicYearId &&
			template.Status == AdmissionCrmRegistrationTemplateStatus.Published));
		var documentQuery = await _documentTemplateRepository.GetQueryableAsync();
		var hasPublishedDocuments = await AsyncExecuter.AnyAsync(documentQuery.Where(template =>
			template.TenantId == tenantId &&
			template.AcademicYearId == academicYearId &&
			template.Status == AdmissionCrmDocumentChecklistTemplateStatus.Published));
		var messageQuery = await _messageTemplateRepository.GetQueryableAsync();
		var hasApprovedMessage = await AsyncExecuter.AnyAsync(messageQuery.Where(template =>
			template.TenantId == tenantId &&
			template.AcademicYearId == academicYearId &&
			template.Status == AdmissionCrmMessageTemplateStatus.Approved &&
			template.IsActive));

		var admissionYearReady = publishedAdmissionYear is not null;
		var pipelineReady = hasActiveSource && hasActiveType;
		var examReady = admissionYearReady &&
			(!publishedAdmissionYear!.EntranceExamRequired || hasPublishedExamPolicy);
		var registrationReady = hasPublishedRegistration && hasPublishedDocuments;
		var financeReady = admissionYearReady;
		var placementReady = admissionYearReady;
		var activationReady = admissionYearReady;
		var communicationReady = hasApprovedMessage;
		var readiness = new[]
		{
			admissionYearReady,
			pipelineReady,
			examReady,
			registrationReady,
			financeReady,
			placementReady,
			activationReady,
			communicationReady
		};
		var overall = readiness.All(value => value)
			? adoption is not null
				? AdmissionCrmSetupReadiness.Managed
				: AdmissionCrmSetupReadiness.Ready
			: AdmissionCrmSetupReadiness.NeedsAttention;
		var policyByKey = policies.ToDictionary(x => x.SettingsKey, StringComparer.OrdinalIgnoreCase);
		var definitions = new[]
		{
			new SetupDefinition("admission-year", "Admission Year Setup", "/student/enrollment/settings/admission-year", "journey", admissionYearReady, "EnrollmentSetup:PublishedAdmissionYearRequired"),
			new SetupDefinition("pipeline", "Pipeline Settings", "/student/enrollment/settings/pipeline", "inquiry-capture", pipelineReady, "EnrollmentSetup:InquirySourceAndTypeRequired"),
			new SetupDefinition("entrance-exam", "Entrance Exam Settings", "/student/enrollment/settings/entrance-exam", "entrance-exams", examReady, "EnrollmentSetup:PublishedExamPolicyRequired"),
			new SetupDefinition("registration", "Registration Settings", "/student/enrollment/settings/registration", "registration-documents", registrationReady, "EnrollmentSetup:PublishedRegistrationAndDocumentsRequired"),
			new SetupDefinition("finance-handoff", "Finance Handoff Settings", "/student/enrollment/settings/finance-handoff", "journey", financeReady, "EnrollmentSetup:PublishedAdmissionYearRequired"),
			new SetupDefinition("class-placement", "Class Placement Settings", "/student/enrollment/settings/class-placement", "offers-seats", placementReady, "EnrollmentSetup:PublishedAdmissionYearRequired"),
			new SetupDefinition("activation", "Activation Settings", "/student/enrollment/settings/activation", "activation", activationReady, "EnrollmentSetup:PublishedAdmissionYearRequired"),
			new SetupDefinition("communication", "Communication Settings", "/student/enrollment/settings/communication", "communications", communicationReady, "EnrollmentSetup:ApprovedMessageTemplateRequired")
		};

		return new AdmissionCrmSetupStatusDto
		{
			AcademicYearId = academicYearId,
			CycleName = configuration.CycleName,
			Status = overall,
			AppliedPresetName = preset?.Name,
			AppliedPresetVersion = adoption?.PresetVersion,
			LastUpdatedAt = publishedAdmissionYear?.LastModificationTime ??
				publishedAdmissionYear?.CreationTime ??
				adoption?.AppliedAt,
			Items = definitions.Select(item =>
			{
				var mode = policyByKey.GetValueOrDefault(item.PolicyKey)?.AccessMode ??
					AdmissionCrmSettingsAccessMode.Editable;
				var status = mode == AdmissionCrmSettingsAccessMode.Hidden
					? AdmissionCrmSetupReadiness.Locked
					: item.Ready
						? adoption is not null
							? AdmissionCrmSetupReadiness.Managed
							: AdmissionCrmSetupReadiness.Ready
						: AdmissionCrmSetupReadiness.NeedsAttention;
				return new AdmissionCrmSetupItemDto
				{
					Key = item.Key,
					Title = item.Title,
					Path = item.Path,
					AccessMode = mode,
					Status = status,
					BlockerCodes = status == AdmissionCrmSetupReadiness.NeedsAttention
						? [item.BlockerCode]
						: []
				};
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
		var journeyRules = configuration.JourneyRules;
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
			Communications = new AdmissionCrmCycleCommunicationSummaryDto
			{
				JourneyRuleCount = journeyRules.Count,
				ActiveMessageRuleCount = journeyRules.Count(x => x.IsEnabled && x.SendMessage),
				TemplateBackedRuleCount = journeyRules.Count(x => x.IsEnabled && x.SendMessage && x.MessageTemplateId.HasValue)
			},
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

	private string GetAdmissionYearLockKey(Guid academicYearId) =>
		$"AdmissionCrm:AdmissionYear:{CurrentTenant.Id?.ToString("D") ?? "host"}:{academicYearId:D}";

	private static void EnsureAdmissionYearConcurrency(
		AdmissionCrmAdmissionYearSetup setup,
		string concurrencyStamp)
	{
		if (string.Equals(setup.ConcurrencyStamp, concurrencyStamp, StringComparison.Ordinal))
		{
			return;
		}

		throw new BusinessException("AdmissionCrm:AdmissionYearDraftChanged")
			.WithData("SetupId", setup.Id)
			.WithData("Version", setup.Version);
	}

	private async Task<List<AdmissionYearStandardRow>> GetAdmissionYearStandardRowsAsync(
		Guid academicYearId)
	{
		var standards = await _standardRepository.GetQueryableAsync();
		var stages = await _mediumStageRepository.GetQueryableAsync();
		var mediums = await _mediumRepository.GetQueryableAsync();
		var boards = await _boardRepository.GetQueryableAsync();
		var query =
			from standard in standards
			join stage in stages on standard.EducationMediumStageId equals stage.Id
			join medium in mediums on stage.EducationMediumId equals medium.Id
			join board in boards on medium.EducationBoardId equals board.Id
			where board.AcademicYearId == academicYearId
			select new AdmissionYearStandardRow(
				standard.Id,
				standard.Name,
				stage.Id,
				stage.Name,
				medium.Id,
				medium.Name,
				board.Id,
				board.Type);
		return await AsyncExecuter.ToListAsync(query);
	}

	private async Task<AdmissionCrmAdmissionYearSetup> GetAdmissionYearForCurrentTenantAsync(
		Guid id,
		string operation)
	{
		var setup = await _admissionYearRepository.FindAsync(id);
		if (setup is null || setup.TenantId != CurrentTenant.Id)
		{
			throw new BusinessException("AdmissionCrm:AdmissionYearSetupNotFound")
				.WithData("SetupId", id)
				.WithData("Operation", operation);
		}

		return setup;
	}

	private async Task ValidateAdmissionYearInputAsync(
		Guid academicYearId,
		UpdateAdmissionCrmAdmissionYearSetupDto input,
		bool requirePublishReady = false)
	{
		if (!Enum.IsDefined(input.PlacementTiming))
		{
			throw new BusinessException("AdmissionCrm:InvalidPlacementTiming")
				.WithData("PlacementTiming", input.PlacementTiming);
		}

		if (input.TargetStandardIds.Count != input.TargetStandardIds.Distinct().Count())
		{
			throw new BusinessException("AdmissionCrm:DuplicateAdmissionYearStandard");
		}

		if (input.RouteTypes.Count != input.RouteTypes.Distinct().Count() ||
			input.RouteTypes.Any(route => !Enum.IsDefined(route)))
		{
			throw new BusinessException("AdmissionCrm:InvalidAdmissionYearRoutes");
		}

		if (requirePublishReady && input.TargetStandardIds.Count == 0)
		{
			throw new BusinessException("AdmissionCrm:AdmissionYearStandardsRequired");
		}

		if (requirePublishReady && input.RouteTypes.Count == 0)
		{
			throw new BusinessException("AdmissionCrm:AdmissionYearRoutesRequired");
		}

		if (requirePublishReady)
		{
			var sourceQuery = await _inquirySourceRepository.GetQueryableAsync();
			var hasActiveSource = await AsyncExecuter.AnyAsync(sourceQuery.Where(source =>
				source.TenantId == CurrentTenant.Id &&
				source.AcademicYearId == academicYearId &&
				source.IsActive));
			if (!hasActiveSource)
			{
				throw new BusinessException("AdmissionCrm:ActiveInquirySourceRequired");
			}

			var typeQuery = await _inquiryTypeRepository.GetQueryableAsync();
			var hasActiveType = await AsyncExecuter.AnyAsync(typeQuery.Where(type =>
				type.TenantId == CurrentTenant.Id &&
				type.AcademicYearId == academicYearId &&
				type.IsActive));
			if (!hasActiveType)
			{
				throw new BusinessException("AdmissionCrm:ActiveInquiryTypeRequired");
			}
		}

		var standardRows = await GetAdmissionYearStandardRowsAsync(academicYearId);
		var academicYearStandardIds = standardRows
			.Select(row => row.StandardId)
			.ToHashSet();
		var invalidStandardIds = input.TargetStandardIds
			.Where(standardId => !academicYearStandardIds.Contains(standardId))
			.ToList();
		if (invalidStandardIds.Count > 0)
		{
			throw new BusinessException("AdmissionCrm:StandardNotInAdmissionYear")
				.WithData("AcademicYearId", academicYearId)
				.WithData("InvalidStandardCount", invalidStandardIds.Count);
		}

		foreach (var standardId in input.TargetStandardIds)
		{
			await _scopeAccess.EnsureCanAccessStandardAsync(
				standardId,
				nameof(ValidateAdmissionYearInputAsync));
		}

		if (input.StepOwners.Keys.Any(key => !AdmissionYearStepKeys.Contains(key)))
		{
			throw new BusinessException("AdmissionCrm:InvalidAdmissionYearStepOwner");
		}

		var ownerIds = input.StepOwners.Values
			.Append(input.DefaultOwnerUserId)
			.Where(ownerId => ownerId.HasValue)
			.Select(ownerId => ownerId!.Value)
			.Distinct();
		foreach (var ownerId in ownerIds)
		{
			if (!await _staffEligibilityService.IsOwnerEligibleAsync(ownerId, academicYearId))
			{
				throw new BusinessException("AdmissionCrm:IneligibleAdmissionYearOwner")
					.WithData("OwnerUserId", ownerId)
					.WithData("AcademicYearId", academicYearId);
			}
		}
	}

	private sealed record AdmissionYearStandardRow(
		Guid StandardId,
		string StandardName,
		Guid MediumStageId,
		string MediumStageName,
		Guid MediumId,
		string MediumName,
		Guid BoardId,
		EducationBoardType BoardType);

	private sealed record SetupDefinition(
		string Key,
		string Title,
		string Path,
		string PolicyKey,
		bool Ready,
		string BlockerCode);

	private static AdmissionCrmAdmissionYearSetupDto ToAdmissionYearDto(
		AdmissionCrmAdmissionYearSetup setup)
	{
		return new AdmissionCrmAdmissionYearSetupDto
		{
			Id = setup.Id,
			ConcurrencyStamp = setup.ConcurrencyStamp,
			AcademicYearId = setup.AcademicYearId,
			Version = setup.Version,
			Status = setup.Status,
			InquiryOpenDate = setup.InquiryOpenDate,
			InquiryCloseDate = setup.InquiryCloseDate,
			RegistrationCloseDate = setup.RegistrationCloseDate,
			ActivationTargetDate = setup.ActivationTargetDate,
			EntranceExamRequired = setup.EntranceExamRequired,
			FinanceVerificationRequired = setup.FinanceVerificationRequired,
			PlacementTiming = setup.PlacementTiming,
			DefaultOwnerUserId = setup.DefaultOwnerUserId,
			TargetStandardIds =
				JsonSerializer.Deserialize<List<Guid>>(setup.TargetStandardIdsJson, JsonOptions) ?? [],
			RouteTypes =
				JsonSerializer.Deserialize<List<AdmissionCrmRouteType>>(setup.RouteTypesJson, JsonOptions) ?? [],
			StepOwners =
				JsonSerializer.Deserialize<Dictionary<string, Guid?>>(setup.StepOwnersJson, JsonOptions) ??
				new Dictionary<string, Guid?>(),
			PublishedAt = setup.PublishedAt,
			PublishedByUserId = setup.PublishedByUserId,
			ClosedAt = setup.ClosedAt,
			ClosedByUserId = setup.ClosedByUserId,
			CloseReason = setup.CloseReason
		};
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
			CycleName = configuration?.CycleName ?? $"{year.Name} Admission Year",
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
			warnings.Add("Exam stages will be hidden from the configured journey because this admission year does not require an entrance exam.");
		}

		if (current.Stages.Any(stage =>
			candidate.Stages.Any(next =>
				next.StageKey == stage.StageKey &&
				next.DisplayOrder != stage.DisplayOrder)))
		{
			warnings.Add("Stage ordering changes how administrators read this admission year journey; it does not migrate applicant history.");
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
