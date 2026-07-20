using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[Authorize]
[RemoteService(false)]
public class AdmissionCrmConfigurationAppService : ApplicationService, IAdmissionCrmConfigurationAppService
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	private readonly IRepository<AdmissionCrmCycleConfiguration, Guid> _cycleRepository;
	private readonly IRepository<AdmissionCrmPipelineStage, Guid> _stageRepository;
	private readonly IRepository<AdmissionInquiryFormTemplate, Guid> _formTemplateRepository;
	private readonly IRepository<AdmissionCrmMessageTemplate, Guid> _messageTemplateRepository;
	private readonly StudentScopeAccessService _studentScopeAccessService;
	private readonly AdmissionCrmStaffEligibilityService _staffEligibilityService;

	public AdmissionCrmConfigurationAppService(
		IRepository<AdmissionCrmCycleConfiguration, Guid> cycleRepository,
		IRepository<AdmissionCrmPipelineStage, Guid> stageRepository,
		IRepository<AdmissionInquiryFormTemplate, Guid> formTemplateRepository,
		IRepository<AdmissionCrmMessageTemplate, Guid> messageTemplateRepository,
		StudentScopeAccessService studentScopeAccessService,
		AdmissionCrmStaffEligibilityService staffEligibilityService)
	{
		_cycleRepository = cycleRepository;
		_stageRepository = stageRepository;
		_formTemplateRepository = formTemplateRepository;
		_messageTemplateRepository = messageTemplateRepository;
		_studentScopeAccessService = studentScopeAccessService;
		_staffEligibilityService = staffEligibilityService;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<AdmissionCrmCycleConfigurationDto> GetSchoolDefaultCycleConfigurationAsync()
	{
		var configuration = await EnsureSchoolDefaultCycleConfigurationAsync();
		return ToDto(configuration);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<AdmissionCrmCycleConfigurationDto> UpdateSchoolDefaultCycleConfigurationAsync(
		UpdateAdmissionCrmCycleConfigurationDto input)
	{
		ValidateSlaPolicy(input.SlaPolicy);
		await _staffEligibilityService.ValidateConfigurationStaffAsync(
			input.LeadRoutingSettings,
			input.ChannelSettings,
			input.JourneyRules,
			input.SlaPolicy);
		var configuration = await EnsureSchoolDefaultCycleConfigurationAsync();
		configuration.Update(
			input.CycleName,
			input.IsActive,
			input.RegistrationFormRequiredBeforeExam,
			input.EntranceExamRequired,
			input.StaleAfterDays,
			input.ArchiveAfterDays,
			input.DefaultLeadSlaHours,
			input.ActivationCancellationWindowDays,
			input.DefaultExamPassingPercentage,
			input.MeritRankingMode,
			input.ExamReadinessFields,
			AdmissionCrmLeadScoreCalculator.SerializeRules(input.LeadScoringRules),
			AdmissionCrmLeadRoutingRuleMatcher.SerializeSettings(input.LeadRoutingSettings),
			AdmissionCrmJourneyRuleSerializer.SerializeRules(input.JourneyRules),
			AdmissionCrmApplicantChecklistSerializer.SerializeItems(input.ApplicantChecklist),
			AdmissionCrmChannelSettingsSerializer.SerializeSettings(input.ChannelSettings),
			JsonSerializer.Serialize(input.SlaPolicy),
			input.Notes);

		await _cycleRepository.UpdateAsync(configuration, autoSave: true);
		return ToDto(configuration);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<AdmissionCrmCycleConfigurationDto> GetCycleConfigurationAsync(Guid academicYearId)
	{
		var configuration = await EnsureCycleConfigurationAsync(academicYearId);
		return ToDto(configuration);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<AdmissionCrmCycleConfigurationDto> UpdateCycleConfigurationAsync(
		Guid academicYearId,
		UpdateAdmissionCrmCycleConfigurationDto input)
	{
		ValidateSlaPolicy(input.SlaPolicy);
		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(
			academicYearId,
			nameof(UpdateCycleConfigurationAsync));
		await _staffEligibilityService.ValidateConfigurationStaffAsync(
			input.LeadRoutingSettings,
			input.ChannelSettings,
			input.JourneyRules,
			input.SlaPolicy);
		var configuration = await EnsureCycleConfigurationAsync(academicYearId);
		configuration.Update(
			input.CycleName,
			input.IsActive,
			input.RegistrationFormRequiredBeforeExam,
			input.EntranceExamRequired,
			input.StaleAfterDays,
			input.ArchiveAfterDays,
			input.DefaultLeadSlaHours,
			input.ActivationCancellationWindowDays,
			input.DefaultExamPassingPercentage,
			input.MeritRankingMode,
			input.ExamReadinessFields,
			AdmissionCrmLeadScoreCalculator.SerializeRules(input.LeadScoringRules),
			AdmissionCrmLeadRoutingRuleMatcher.SerializeSettings(input.LeadRoutingSettings),
			AdmissionCrmJourneyRuleSerializer.SerializeRules(input.JourneyRules),
			AdmissionCrmApplicantChecklistSerializer.SerializeItems(input.ApplicantChecklist),
			AdmissionCrmChannelSettingsSerializer.SerializeSettings(input.ChannelSettings),
			JsonSerializer.Serialize(input.SlaPolicy),
			input.Notes);

		await _cycleRepository.UpdateAsync(configuration, autoSave: true);
		return ToDto(configuration);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<PagedResultDto<AdmissionCrmPipelineStageDto>> GetPipelineStagesAsync(Guid academicYearId)
	{
		var configuration = await EnsureCycleConfigurationAsync(academicYearId);
		var stages = await EnsurePipelineStagesAsync(academicYearId);
		var ordered = stages
			.OrderBy(x => x.DisplayOrder)
			.ThenBy(x => x.SystemStage)
			.Select(x => ToDto(x, configuration.JourneyLocked))
			.ToList();

		return new PagedResultDto<AdmissionCrmPipelineStageDto>(ordered.Count, ordered);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<AdmissionCrmPipelineStageDto> CreatePipelineStageAsync(
		Guid academicYearId,
		CreateAdmissionCrmPipelineStageDto input)
	{
		var configuration = await EnsureCycleConfigurationAsync(academicYearId);
		EnsureJourneyStructureEditable(configuration);
		await EnsureUniqueStageKeyAsync(academicYearId, input.StageKey, excludeId: null);
		var allowedNextStagesJson = SerializeAllowedNextStages(input.AllowedNextStages ?? []);
		var stage = AdmissionCrmPipelineStage.CreateCustom(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			academicYearId,
			input.StageKey,
			input.BaseSystemStage,
			input.StageName,
			input.DisplayOrder,
			input.ColorHex,
			input.SlaTargetHours,
			input.OwnerResponsibility,
			allowedNextStagesJson);
		await _stageRepository.InsertAsync(stage, autoSave: true);
		return ToDto(stage, configuration.JourneyLocked);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<AdmissionCrmPipelineStageDto> UpdatePipelineStageAsync(
		Guid stageId,
		UpdateAdmissionCrmPipelineStageDto input)
	{
		var stage = await GetStageForCurrentTenantAsync(stageId, nameof(UpdatePipelineStageAsync));
		var configuration = await EnsureCycleConfigurationAsync(stage.AcademicYearId);
		if (configuration.JourneyLocked &&
		    (input.StageName != stage.StageName ||
		     input.DisplayOrder != stage.DisplayOrder ||
		     input.IsEnabled != stage.IsEnabled ||
		     input.AllowedNextStages != null))
		{
			throw new BusinessException("AdmissionCrm:JourneyLocked");
		}
		var allowedNextStagesJson = input.AllowedNextStages is null
			? stage.AllowedNextStagesJson
			: SerializeAllowedNextStages(input.AllowedNextStages);

		stage.Update(
			input.StageName,
			input.DisplayOrder,
			input.IsEnabled,
			input.ColorHex ?? stage.ColorHex,
			input.SlaTargetHours ?? stage.SlaTargetHours,
			input.OwnerResponsibility ?? stage.OwnerResponsibility,
			allowedNextStagesJson);
		await _stageRepository.UpdateAsync(stage, autoSave: true);
		return ToDto(stage, configuration.JourneyLocked);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<AdmissionCrmCycleConfigurationDto> LockJourneyAsync(Guid academicYearId, AdmissionCrmJourneyLockDto input)
	{
		var configuration = await EnsureCycleConfigurationAsync(academicYearId);
		configuration.LockJourney(CurrentUser.Id, Clock.Now, input.Reason);
		await _cycleRepository.UpdateAsync(configuration, autoSave: true);
		return ToDto(configuration);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<AdmissionCrmCycleConfigurationDto> UnlockJourneyAsync(Guid academicYearId, AdmissionCrmJourneyLockDto input)
	{
		if (string.IsNullOrWhiteSpace(input.Reason))
		{
			throw new BusinessException("AdmissionCrm:JourneyUnlockReasonRequired");
		}
		var configuration = await EnsureCycleConfigurationAsync(academicYearId);
		configuration.UnlockJourney();
		await _cycleRepository.UpdateAsync(configuration, autoSave: true);
		return ToDto(configuration);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<AdmissionCrmFormTemplateDto> CreateFormTemplateAsync(
		Guid academicYearId,
		CreateAdmissionCrmFormTemplateDto input)
	{
		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(
			academicYearId,
			nameof(CreateFormTemplateAsync));
		await EnsureTemplateNameIsUniqueAsync(academicYearId, input.TemplateName, excludeId: null);

		var fieldsJson = SerializeFields(input.Fields);
		var template = new AdmissionInquiryFormTemplate(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			academicYearId,
			input.TemplateName,
			input.Description,
			input.Channel,
			input.IsPublic,
			input.ConsentText,
			input.PrivacyNoticeUrl,
				fieldsJson,
				JsonSerializer.Serialize(input.Localizations));

		await _formTemplateRepository.InsertAsync(template, autoSave: true);
		return ToDto(template);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<PagedResultDto<AdmissionCrmFormTemplateDto>> GetFormTemplatesAsync(
		Guid academicYearId,
		GetAdmissionCrmFormTemplateListInput input)
	{
		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(
			academicYearId,
			nameof(GetFormTemplatesAsync));

		var queryable = await _formTemplateRepository.GetQueryableAsync();
		var query = queryable.Where(x =>
			x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == academicYearId);

		if (!string.IsNullOrWhiteSpace(input.FilterText))
		{
			var filter = input.FilterText.Trim().ToLowerInvariant();
			query = query.Where(x =>
				x.TemplateName.ToLower().Contains(filter) ||
				(x.Description != null && x.Description.ToLower().Contains(filter)));
		}

		if (input.IsActive.HasValue)
		{
			query = query.Where(x => x.IsActive == input.IsActive.Value);
		}

		if (input.IsPublic.HasValue)
		{
			query = query.Where(x => x.IsPublic == input.IsPublic.Value);
		}

		if (input.Channel.HasValue)
		{
			query = query.Where(x => x.Channel == input.Channel.Value);
		}

		var totalCount = await AsyncExecuter.CountAsync(query);
		var items = await AsyncExecuter.ToListAsync(
			query
				.OrderByDescending(x => x.IsActive)
				.ThenBy(x => x.TemplateName)
				.Skip(input.SkipCount)
				.Take(input.MaxResultCount));

		return new PagedResultDto<AdmissionCrmFormTemplateDto>(
			totalCount,
			items.Select(ToDto).ToList());
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<AdmissionCrmFormTemplateDto> GetFormTemplateAsync(Guid id)
	{
		return ToDto(await GetFormTemplateForCurrentTenantAsync(id, nameof(GetFormTemplateAsync)));
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<AdmissionCrmFormTemplateDto> UpdateFormTemplateAsync(
		Guid id,
		UpdateAdmissionCrmFormTemplateDto input)
	{
		var template = await GetFormTemplateForCurrentTenantAsync(id, nameof(UpdateFormTemplateAsync));
		await EnsureTemplateNameIsUniqueAsync(template.AcademicYearId, input.TemplateName, excludeId: template.Id);

		template.Update(
			input.TemplateName,
			input.Description,
			input.Channel,
			input.IsPublic,
			input.ConsentText,
			input.PrivacyNoticeUrl,
				SerializeFields(input.Fields),
				JsonSerializer.Serialize(input.Localizations));

		await _formTemplateRepository.UpdateAsync(template, autoSave: true);
		return ToDto(template);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<AdmissionCrmFormTemplateDto> UpdateFormTemplateStatusAsync(
		Guid id,
		UpdateAdmissionCrmFormTemplateStatusDto input)
	{
		var template = await GetFormTemplateForCurrentTenantAsync(id, nameof(UpdateFormTemplateStatusAsync));
		if (input.IsActive)
		{
			template.Activate();
		}
		else
		{
			template.Deactivate();
		}

		await _formTemplateRepository.UpdateAsync(template, autoSave: true);
		return ToDto(template);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageTemplates)]
	public async Task<AdmissionCrmMessageTemplateDto> CreateMessageTemplateAsync(
		Guid academicYearId,
		CreateAdmissionCrmMessageTemplateDto input)
	{
		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(
			academicYearId,
			nameof(CreateMessageTemplateAsync));
		await EnsureMessageTemplateNameIsUniqueAsync(
			academicYearId,
			input.Channel,
			input.TemplateName,
			excludeId: null);

		var template = new AdmissionCrmMessageTemplate(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			academicYearId,
			input.TemplateName,
			input.Channel,
			input.Purpose,
			input.Subject,
			input.Body,
			SerializeVariables(input.Variables));

		await _messageTemplateRepository.InsertAsync(template, autoSave: true);
		return ToDto(template);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageTemplates)]
	public async Task<PagedResultDto<AdmissionCrmMessageTemplateDto>> GetMessageTemplatesAsync(
		Guid academicYearId,
		GetAdmissionCrmMessageTemplateListInput input)
	{
		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(
			academicYearId,
			nameof(GetMessageTemplatesAsync));

		var queryable = await _messageTemplateRepository.GetQueryableAsync();
		var query = queryable.Where(x =>
			x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == academicYearId);

		if (!string.IsNullOrWhiteSpace(input.FilterText))
		{
			var filter = input.FilterText.Trim().ToLowerInvariant();
			query = query.Where(x =>
				x.TemplateName.ToLower().Contains(filter) ||
				x.Purpose.ToLower().Contains(filter) ||
				x.Body.ToLower().Contains(filter) ||
				(x.Subject != null && x.Subject.ToLower().Contains(filter)));
		}

		if (input.Channel.HasValue)
		{
			query = query.Where(x => x.Channel == input.Channel.Value);
		}

		if (input.Status.HasValue)
		{
			query = query.Where(x => x.Status == input.Status.Value);
		}

		if (input.IsActive.HasValue)
		{
			query = query.Where(x => x.IsActive == input.IsActive.Value);
		}

		var totalCount = await AsyncExecuter.CountAsync(query);
		var items = await AsyncExecuter.ToListAsync(
			query
				.OrderByDescending(x => x.IsActive)
				.ThenBy(x => x.Channel)
				.ThenBy(x => x.TemplateName)
				.Skip(input.SkipCount)
				.Take(input.MaxResultCount));

		return new PagedResultDto<AdmissionCrmMessageTemplateDto>(
			totalCount,
			items.Select(ToDto).ToList());
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageTemplates)]
	public async Task<AdmissionCrmMessageTemplateDto> GetMessageTemplateAsync(Guid id)
	{
		return ToDto(await GetMessageTemplateForCurrentTenantAsync(id, nameof(GetMessageTemplateAsync)));
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageTemplates)]
	public async Task<AdmissionCrmMessageTemplateDto> UpdateMessageTemplateAsync(
		Guid id,
		UpdateAdmissionCrmMessageTemplateDto input)
	{
		var template = await GetMessageTemplateForCurrentTenantAsync(id, nameof(UpdateMessageTemplateAsync));
		await EnsureMessageTemplateNameIsUniqueAsync(
			template.AcademicYearId,
			input.Channel,
			input.TemplateName,
			excludeId: template.Id);

		template.Update(
			input.TemplateName,
			input.Channel,
			input.Purpose,
			input.Subject,
			input.Body,
			SerializeVariables(input.Variables));

		await _messageTemplateRepository.UpdateAsync(template, autoSave: true);
		return ToDto(template);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageTemplates)]
	public async Task<AdmissionCrmMessageTemplateDto> UpdateMessageTemplateStatusAsync(
		Guid id,
		UpdateAdmissionCrmMessageTemplateStatusDto input)
	{
		var template = await GetMessageTemplateForCurrentTenantAsync(id, nameof(UpdateMessageTemplateStatusAsync));
		template.UpdateStatus(input.Status, input.IsActive);
		await _messageTemplateRepository.UpdateAsync(template, autoSave: true);
		return ToDto(template);
	}

	private async Task<AdmissionCrmCycleConfiguration> EnsureCycleConfigurationAsync(Guid academicYearId)
	{
		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(
			academicYearId,
			nameof(EnsureCycleConfigurationAsync));

		var queryable = await _cycleRepository.GetQueryableAsync();
		var existing = await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == academicYearId));
		if (existing != null)
		{
			return existing;
		}

		var configuration = new AdmissionCrmCycleConfiguration(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			academicYearId,
			"Default Admission Cycle");
		var schoolDefault = await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == Guid.Empty));
		if (schoolDefault != null)
		{
			configuration.CopySettingsFrom(schoolDefault, schoolDefault.CycleName);
		}

		await _cycleRepository.InsertAsync(configuration, autoSave: true);
		return configuration;
	}

	private async Task<AdmissionCrmCycleConfiguration> EnsureSchoolDefaultCycleConfigurationAsync()
	{
		var queryable = await _cycleRepository.GetQueryableAsync();
		var existing = await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == Guid.Empty));
		if (existing != null)
		{
			return existing;
		}

		var configuration = new AdmissionCrmCycleConfiguration(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			Guid.Empty,
			"School Admissions CRM Defaults");

		await _cycleRepository.InsertAsync(configuration, autoSave: true);
		return configuration;
	}

	private async Task<IReadOnlyList<AdmissionCrmPipelineStage>> EnsurePipelineStagesAsync(Guid academicYearId)
	{
		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(
			academicYearId,
			nameof(EnsurePipelineStagesAsync));

		var queryable = await _stageRepository.GetQueryableAsync();
		var existing = await AsyncExecuter.ToListAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == academicYearId));
		if (existing.Count > 0)
		{
			return existing;
		}

		var defaults = GetDefaultPipelineStages()
			.Select(x => new AdmissionCrmPipelineStage(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				academicYearId,
				x.SystemStage,
				x.StageName,
				x.DisplayOrder,
				x.ColorHex,
				x.SlaTargetHours,
				x.OwnerResponsibility,
				SerializeAllowedNextStages(x.AllowedNextStages)))
			.ToList();

		foreach (var stage in defaults)
		{
			await _stageRepository.InsertAsync(stage, autoSave: false);
		}

		await CurrentUnitOfWork.SaveChangesAsync();
		return defaults;
	}

	private async Task<AdmissionCrmPipelineStage> GetStageForCurrentTenantAsync(Guid id, string operation)
	{
		var stage = await _stageRepository.GetAsync(id);
		if (stage.TenantId != CurrentTenant.Id)
		{
			throw new BusinessException("AdmissionCrm:PipelineStageNotFound")
				.WithData("StageId", id);
		}

		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(stage.AcademicYearId, operation);
		return stage;
	}

	private async Task EnsureUniqueStageKeyAsync(Guid academicYearId, string stageKey, Guid? excludeId)
	{
		var normalized = stageKey.Trim().ToUpperInvariant();
		var queryable = await _stageRepository.GetQueryableAsync();
		if (await AsyncExecuter.AnyAsync(queryable.Where(x =>
			    x.TenantId == CurrentTenant.Id &&
			    x.AcademicYearId == academicYearId &&
			    x.StageKey == normalized &&
			    (!excludeId.HasValue || x.Id != excludeId.Value))))
		{
			throw new BusinessException("AdmissionCrm:DuplicatePipelineStageKey")
				.WithData("StageKey", normalized);
		}
	}

	private static void EnsureJourneyStructureEditable(AdmissionCrmCycleConfiguration configuration)
	{
		if (configuration.JourneyLocked)
		{
			throw new BusinessException("AdmissionCrm:JourneyLocked");
		}
	}

	private async Task<AdmissionInquiryFormTemplate> GetFormTemplateForCurrentTenantAsync(Guid id, string operation)
	{
		var template = await _formTemplateRepository.GetAsync(id);
		if (template.TenantId != CurrentTenant.Id)
		{
			throw new BusinessException("AdmissionCrm:FormTemplateNotFound")
				.WithData("TemplateId", id);
		}

		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(template.AcademicYearId, operation);
		return template;
	}

	private async Task<AdmissionCrmMessageTemplate> GetMessageTemplateForCurrentTenantAsync(
		Guid id,
		string operation)
	{
		var template = await _messageTemplateRepository.GetAsync(id);
		if (template.TenantId != CurrentTenant.Id)
		{
			throw new BusinessException("AdmissionCrm:MessageTemplateNotFound")
				.WithData("TemplateId", id);
		}

		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(template.AcademicYearId, operation);
		return template;
	}

	private async Task EnsureTemplateNameIsUniqueAsync(Guid academicYearId, string templateName, Guid? excludeId)
	{
		var normalizedName = templateName.Trim().ToLowerInvariant();
		var queryable = await _formTemplateRepository.GetQueryableAsync();
		var exists = await AsyncExecuter.AnyAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == academicYearId &&
			x.TemplateName.ToLower() == normalizedName &&
			(!excludeId.HasValue || x.Id != excludeId.Value)));

		if (exists)
		{
			throw new BusinessException("AdmissionCrm:DuplicateFormTemplateName")
				.WithData("TemplateName", templateName);
		}
	}

	private async Task EnsureMessageTemplateNameIsUniqueAsync(
		Guid academicYearId,
		AdmissionCrmMessageChannel channel,
		string templateName,
		Guid? excludeId)
	{
		var normalizedName = templateName.Trim().ToLowerInvariant();
		var queryable = await _messageTemplateRepository.GetQueryableAsync();
		var exists = await AsyncExecuter.AnyAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == academicYearId &&
			x.Channel == channel &&
			x.TemplateName.ToLower() == normalizedName &&
			(!excludeId.HasValue || x.Id != excludeId.Value)));

		if (exists)
		{
			throw new BusinessException("AdmissionCrm:DuplicateMessageTemplateName")
				.WithData("TemplateName", templateName)
				.WithData("Channel", channel);
		}
	}

	private static string SerializeFields(IReadOnlyList<AdmissionCrmFormFieldDto> fields)
	{
		if (fields.Count == 0)
		{
			throw new BusinessException("AdmissionCrm:FormFieldsRequired");
		}

		var duplicateKeys = fields
			.GroupBy(x => x.Key.Trim().ToLowerInvariant())
			.Where(x => x.Count() > 1)
			.Select(x => x.Key)
			.ToList();

		if (duplicateKeys.Count > 0)
		{
			throw new BusinessException("AdmissionCrm:DuplicateFormFieldKey")
				.WithData("FieldKey", duplicateKeys[0]);
		}

		var orderedFields = fields
			.OrderBy(x => x.DisplayOrder)
			.ThenBy(x => x.Key)
			.ToList();

		return JsonSerializer.Serialize(orderedFields, JsonOptions);
	}

	private static IReadOnlyList<AdmissionCrmFormFieldDto> DeserializeFields(string fieldsJson)
	{
		return JsonSerializer.Deserialize<List<AdmissionCrmFormFieldDto>>(fieldsJson, JsonOptions) ?? [];
	}

	private static string SerializeVariables(IReadOnlyList<string> variables)
	{
		var normalized = variables
			.Select(x => x.Trim())
			.Where(x => !string.IsNullOrWhiteSpace(x))
			.OrderBy(x => x)
			.ToList();
		var duplicate = normalized
			.GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
			.FirstOrDefault(x => x.Count() > 1);
		if (duplicate != null)
		{
			throw new BusinessException("AdmissionCrm:DuplicateMessageTemplateVariable")
				.WithData("Variable", duplicate.Key);
		}

		return JsonSerializer.Serialize(normalized, JsonOptions);
	}

	private static IReadOnlyList<string> DeserializeVariables(string variablesJson)
	{
		return JsonSerializer.Deserialize<List<string>>(variablesJson, JsonOptions) ?? [];
	}

	private static AdmissionCrmCycleConfigurationDto ToDto(AdmissionCrmCycleConfiguration configuration)
	{
		return new AdmissionCrmCycleConfigurationDto
		{
			Id = configuration.Id,
			AcademicYearId = configuration.AcademicYearId,
			CycleName = configuration.CycleName,
			IsActive = configuration.IsActive,
			RegistrationFormRequiredBeforeExam = configuration.RegistrationFormRequiredBeforeExam,
			EntranceExamRequired = configuration.EntranceExamRequired,
			JourneyLocked = configuration.JourneyLocked,
			JourneyLockedAt = configuration.JourneyLockedAt,
			JourneyLockedByUserId = configuration.JourneyLockedByUserId,
			JourneyLockReason = configuration.JourneyLockReason,
			StaleAfterDays = configuration.StaleAfterDays,
			ArchiveAfterDays = configuration.ArchiveAfterDays,
			DefaultLeadSlaHours = configuration.DefaultLeadSlaHours,
			ActivationCancellationWindowDays = configuration.ActivationCancellationWindowDays,
			DefaultExamPassingPercentage = configuration.DefaultExamPassingPercentage,
			MeritRankingMode = configuration.MeritRankingMode,
			ExamReadinessFields = configuration.ExamReadinessFields,
			LeadScoringRules = AdmissionCrmLeadScoreCalculator.DeserializeRules(
				configuration.LeadScoringRulesJson),
			LeadRoutingSettings = AdmissionCrmLeadRoutingRuleMatcher.DeserializeSettings(
				configuration.LeadRoutingRulesJson),
			JourneyRules = AdmissionCrmJourneyRuleSerializer.DeserializeRules(
				configuration.JourneyRulesJson),
			ApplicantChecklist = AdmissionCrmApplicantChecklistSerializer.DeserializeItems(
				configuration.ApplicantChecklistJson),
			ChannelSettings = AdmissionCrmChannelSettingsSerializer.DeserializeSettings(
				configuration.ChannelSettingsJson),
			SlaPolicy = JsonSerializer.Deserialize<AdmissionCrmSlaPolicyDto>(configuration.SlaPolicyJson) ?? new AdmissionCrmSlaPolicyDto(),
			Notes = configuration.Notes
		};
	}

	private static void ValidateSlaPolicy(AdmissionCrmSlaPolicyDto policy)
	{
		if (policy.WorkingDays.Count == 0 || policy.WorkingDays.Count > 7 ||
			policy.WorkingDays.Distinct().Count() != policy.WorkingDays.Count ||
			policy.WorkingDays.Any(day => !Enum.IsDefined(day)))
		{
			throw new BusinessException("AdmissionCrm:InvalidSlaWorkingDays");
		}

		if (policy.WorkingDayStartHour is < 0 or > 23 || policy.WorkingDayEndHour is < 1 or > 24 ||
			policy.WorkingDayEndHour <= policy.WorkingDayStartHour)
		{
			throw new BusinessException("AdmissionCrm:InvalidSlaWorkingHours");
		}

		if (policy.EscalationAfterHours is < 0 or > 720 || policy.Holidays.Count > 366)
		{
			throw new BusinessException("AdmissionCrm:InvalidSlaEscalationPolicy");
		}

		try
		{
			_ = TimeZoneInfo.FindSystemTimeZoneById(policy.TimeZoneId);
		}
		catch (TimeZoneNotFoundException)
		{
			throw new BusinessException("AdmissionCrm:InvalidSlaTimeZone");
		}
		catch (InvalidTimeZoneException)
		{
			throw new BusinessException("AdmissionCrm:InvalidSlaTimeZone");
		}
	}

	private static AdmissionCrmPipelineStageDto ToDto(AdmissionCrmPipelineStage stage, bool journeyLocked)
	{
		return new AdmissionCrmPipelineStageDto
		{
			Id = stage.Id,
			AcademicYearId = stage.AcademicYearId,
			SystemStage = stage.SystemStage,
			StageKey = stage.StageKey,
			BaseSystemStage = stage.BaseSystemStage,
			StageName = stage.StageName,
			DisplayOrder = stage.DisplayOrder,
			IsEnabled = stage.IsEnabled,
			IsSystem = stage.IsSystem,
			StructureLocked = journeyLocked,
			ColorHex = stage.ColorHex,
			SlaTargetHours = stage.SlaTargetHours,
			OwnerResponsibility = stage.OwnerResponsibility,
			AllowedNextStages = DeserializeAllowedNextStages(stage.AllowedNextStagesJson).ToList()
		};
	}

	private static AdmissionCrmFormTemplateDto ToDto(AdmissionInquiryFormTemplate template)
	{
		return new AdmissionCrmFormTemplateDto
		{
			Id = template.Id,
			AcademicYearId = template.AcademicYearId,
			TemplateName = template.TemplateName,
			Description = template.Description,
			Channel = template.Channel,
			IsPublic = template.IsPublic,
			IsActive = template.IsActive,
			ConsentText = template.ConsentText,
			PrivacyNoticeUrl = template.PrivacyNoticeUrl,
				Fields = DeserializeFields(template.FieldsJson),
				Localizations = JsonSerializer.Deserialize<Dictionary<string, AdmissionCrmFormTemplateLocalizationDto>>(template.LocalizationsJson) ?? new Dictionary<string, AdmissionCrmFormTemplateLocalizationDto>()
		};
	}

	private static AdmissionCrmMessageTemplateDto ToDto(AdmissionCrmMessageTemplate template)
	{
		return new AdmissionCrmMessageTemplateDto
		{
			Id = template.Id,
			AcademicYearId = template.AcademicYearId,
			TemplateName = template.TemplateName,
			Channel = template.Channel,
			Purpose = template.Purpose,
			Subject = template.Subject,
			Body = template.Body,
			Variables = DeserializeVariables(template.VariablesJson),
			Status = template.Status,
			IsActive = template.IsActive
		};
	}

	private static string? SerializeAllowedNextStages(IEnumerable<AdmissionInquiryStage>? allowedNextStages)
	{
		if (allowedNextStages is null)
		{
			return null;
		}

		var distinctStages = allowedNextStages
			.Distinct()
			.ToList();
		return JsonSerializer.Serialize(distinctStages, JsonOptions);
	}

	private static IReadOnlyList<AdmissionInquiryStage> DeserializeAllowedNextStages(string? allowedNextStagesJson)
	{
		if (string.IsNullOrWhiteSpace(allowedNextStagesJson))
		{
			return [];
		}

		return JsonSerializer.Deserialize<List<AdmissionInquiryStage>>(allowedNextStagesJson, JsonOptions) ?? [];
	}

	private static IReadOnlyList<DefaultPipelineStage> GetDefaultPipelineStages()
	{
		return
		[
			new DefaultPipelineStage(
				AdmissionInquiryStage.NewInquiry,
				"New Inquiry",
				10,
				"#1976D2",
				24,
				"Admissions counselor",
				[AdmissionInquiryStage.FollowUp, AdmissionInquiryStage.ExamReady, AdmissionInquiryStage.Stale, AdmissionInquiryStage.Archived]),
			new DefaultPipelineStage(
				AdmissionInquiryStage.FollowUp,
				"Follow-up",
				20,
				"#0288D1",
				24,
				"Assigned lead owner",
				[AdmissionInquiryStage.ExamReady, AdmissionInquiryStage.Stale, AdmissionInquiryStage.Archived]),
			new DefaultPipelineStage(
				AdmissionInquiryStage.ExamReady,
				"Exam Ready",
				30,
				"#2E7D32",
				12,
				"Admissions coordinator",
				[AdmissionInquiryStage.ExamScheduled, AdmissionInquiryStage.FollowUp, AdmissionInquiryStage.Archived]),
			new DefaultPipelineStage(
				AdmissionInquiryStage.ExamScheduled,
				"Exam Scheduled",
				40,
				"#6A1B9A",
				null,
				"Exam coordinator",
				[AdmissionInquiryStage.ExamCompleted, AdmissionInquiryStage.FollowUp]),
			new DefaultPipelineStage(
				AdmissionInquiryStage.ExamCompleted,
				"Exam Completed",
				50,
				"#455A64",
				null,
				"Result reviewer",
				[AdmissionInquiryStage.Eligible, AdmissionInquiryStage.Waitlisted, AdmissionInquiryStage.Rejected]),
			new DefaultPipelineStage(
				AdmissionInquiryStage.Eligible,
				"Eligible",
				60,
				"#2E7D32",
				null,
				"Admissions decision owner",
				[]),
			new DefaultPipelineStage(
				AdmissionInquiryStage.Waitlisted,
				"Waitlisted",
				70,
				"#ED6C02",
				null,
				"Admissions decision owner",
				[AdmissionInquiryStage.Eligible, AdmissionInquiryStage.Rejected, AdmissionInquiryStage.Archived]),
			new DefaultPipelineStage(
				AdmissionInquiryStage.Rejected,
				"Rejected",
				80,
				"#D32F2F",
				null,
				"Admissions decision owner",
				[AdmissionInquiryStage.Archived]),
			new DefaultPipelineStage(
				AdmissionInquiryStage.Stale,
				"Stale",
				90,
				"#757575",
				null,
				"Assigned lead owner",
				[AdmissionInquiryStage.FollowUp, AdmissionInquiryStage.Archived]),
			new DefaultPipelineStage(
				AdmissionInquiryStage.Archived,
				"Archived",
				100,
				"#424242",
				null,
				"Admissions administrator",
				[])
		];
	}

	private sealed record DefaultPipelineStage(
		AdmissionInquiryStage SystemStage,
		string StageName,
		int DisplayOrder,
		string ColorHex,
		int? SlaTargetHours,
		string OwnerResponsibility,
		IReadOnlyList<AdmissionInquiryStage> AllowedNextStages);
}
