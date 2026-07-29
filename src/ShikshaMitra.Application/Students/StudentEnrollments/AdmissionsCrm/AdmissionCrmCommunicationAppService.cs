using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[Authorize]
[RemoteService(false)]
public class AdmissionCrmCommunicationAppService : ApplicationService, IAdmissionCrmCommunicationAppService
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
	private readonly IRepository<AdmissionCrmCommunicationProviderConfiguration, Guid> _providerRepository;
	private readonly IRepository<AdmissionCrmCommunicationOptOut, Guid> _optOutRepository;
	private readonly IRepository<AdmissionCrmMessageDeliveryLog, Guid> _deliveryLogRepository;
	private readonly IRepository<AdmissionCrmMessageTemplate, Guid> _templateRepository;
	private readonly IRepository<AdmissionInquiryLead, Guid> _leadRepository;
	private readonly IRepository<AdmissionCrmTimelineEvent, Guid> _timelineRepository;
	private readonly StudentScopeAccessService _studentScopeAccessService;
	private readonly IAdmissionCrmCommunicationDispatcher _dispatcher;

	public AdmissionCrmCommunicationAppService(
		IRepository<AdmissionCrmCommunicationProviderConfiguration, Guid> providerRepository,
		IRepository<AdmissionCrmCommunicationOptOut, Guid> optOutRepository,
		IRepository<AdmissionCrmMessageDeliveryLog, Guid> deliveryLogRepository,
		IRepository<AdmissionCrmMessageTemplate, Guid> templateRepository,
		IRepository<AdmissionInquiryLead, Guid> leadRepository,
		IRepository<AdmissionCrmTimelineEvent, Guid> timelineRepository,
		StudentScopeAccessService studentScopeAccessService,
		IAdmissionCrmCommunicationDispatcher dispatcher)
	{
		_providerRepository = providerRepository;
		_optOutRepository = optOutRepository;
		_deliveryLogRepository = deliveryLogRepository;
		_templateRepository = templateRepository;
		_leadRepository = leadRepository;
		_timelineRepository = timelineRepository;
		_studentScopeAccessService = studentScopeAccessService;
		_dispatcher = dispatcher;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<PagedResultDto<AdmissionCrmCommunicationProviderDto>> GetProvidersAsync(Guid academicYearId)
	{
		await _studentScopeAccessService.EnsureAcademicYearWideConfigurationAccessAsync(
			academicYearId,
			nameof(GetProvidersAsync));

		var providers = await GetOrCreateProvidersAsync(academicYearId);
		return new PagedResultDto<AdmissionCrmCommunicationProviderDto>(
			providers.Count,
			providers.Select(ToDto).ToList());
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<AdmissionCrmCommunicationProviderDto> UpdateProviderAsync(
		Guid academicYearId,
		AdmissionCrmMessageChannel channel,
		UpdateAdmissionCrmCommunicationProviderDto input)
	{
		await _studentScopeAccessService.EnsureAcademicYearWideConfigurationAccessAsync(
			academicYearId,
			nameof(UpdateProviderAsync));

		var provider = await GetOrCreateProviderAsync(academicYearId, channel);
		var settings = MergeProviderSettings(
			DeserializeProviderSettings(provider.SettingsJson),
			input.Settings);
		ValidateProviderEndpoint(settings, input.WebhookUrl);
		provider.Update(
			input.IsEnabled,
			input.ProviderName,
			input.SenderId,
			SerializeProviderSettings(settings),
			input.WebhookUrl,
			input.LastValidatedAt);
		await _providerRepository.UpdateAsync(provider, autoSave: true);
		return ToDto(provider);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageTemplates)]
	public async Task<PagedResultDto<AdmissionCrmCommunicationOptOutDto>> GetOptOutsAsync(
		Guid academicYearId,
		GetAdmissionCrmCommunicationOptOutListInput input)
	{
		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(
			academicYearId,
			nameof(GetOptOutsAsync));
		var hasAcademicYearWideScope =
			await _studentScopeAccessService.CurrentUserHasAcademicYearWideScopeAsync(academicYearId);
		var scopedLeadIds = await GetScopedLeadIdsAsync(academicYearId);

		var queryable = await _optOutRepository.GetQueryableAsync();
		var query = queryable.Where(x =>
			x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == academicYearId &&
			(hasAcademicYearWideScope ||
			 (x.LeadId.HasValue && scopedLeadIds.Contains(x.LeadId.Value))));

		if (input.LeadId.HasValue)
		{
			query = query.Where(x => x.LeadId == input.LeadId.Value);
		}

		if (input.Channel.HasValue)
		{
			query = query.Where(x => x.Channel == input.Channel.Value);
		}

		if (input.IsActive.HasValue)
		{
			query = query.Where(x => x.IsActive == input.IsActive.Value);
		}

		if (!string.IsNullOrWhiteSpace(input.ContactSearch))
		{
			var contactSearch = input.ContactSearch.Trim();
			query = query.Where(x =>
				x.ContactValue.Contains(contactSearch) ||
				x.NormalizedContactValue.Contains(contactSearch));
		}

		var totalCount = await AsyncExecuter.CountAsync(query);
		var optOuts = await AsyncExecuter.ToListAsync(query
			.OrderByDescending(x => x.OptedOutAt)
			.Skip(input.SkipCount)
			.Take(input.MaxResultCount));

		return new PagedResultDto<AdmissionCrmCommunicationOptOutDto>(
			totalCount,
			optOuts.Select(ToDto).ToList());
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageTemplates)]
	public async Task<AdmissionCrmCommunicationOptOutDto> CreateOptOutAsync(
		Guid academicYearId,
		CreateAdmissionCrmCommunicationOptOutDto input)
	{
		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(
			academicYearId,
			nameof(CreateOptOutAsync));
		await EnsureLeadBelongsToAcademicYearAsync(input.LeadId, academicYearId, nameof(CreateOptOutAsync));

		var normalizedContact = AdmissionCrmCommunicationOptOut.NormalizeContact(
			input.Channel,
			input.ContactValue);
		var existing = await GetOptOutOrDefaultAsync(academicYearId, input.Channel, normalizedContact);
		if (existing is null)
		{
			existing = new AdmissionCrmCommunicationOptOut(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				academicYearId,
				input.LeadId,
				input.Channel,
				input.ContactValue,
				input.Reason,
				Clock.Now);
			await _optOutRepository.InsertAsync(existing, autoSave: true);
		}
		else
		{
			existing.Reactivate(input.LeadId, input.Reason, Clock.Now);
			await _optOutRepository.UpdateAsync(existing, autoSave: true);
		}

		return ToDto(existing);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageTemplates)]
	public async Task<AdmissionCrmCommunicationOptOutDto> ClearOptOutAsync(
		Guid academicYearId,
		ClearAdmissionCrmCommunicationOptOutDto input)
	{
		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(
			academicYearId,
			nameof(ClearOptOutAsync));
		await EnsureLeadBelongsToAcademicYearAsync(input.LeadId, academicYearId, nameof(ClearOptOutAsync));

		var normalizedContact = AdmissionCrmCommunicationOptOut.NormalizeContact(
			input.Channel,
			input.ContactValue);
		var optOut = await GetOptOutOrDefaultAsync(academicYearId, input.Channel, normalizedContact);
		if (optOut is null || !optOut.IsActive)
		{
			throw new BusinessException("AdmissionCrm:OptOutNotFound")
				.WithData("Channel", input.Channel)
				.WithData("ContactValue", input.ContactValue);
		}

		optOut.Clear(input.Reason, Clock.Now);
		await _optOutRepository.UpdateAsync(optOut, autoSave: true);
		return ToDto(optOut);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageTemplates)]
	public async Task<AdmissionCrmMessageDeliveryLogDto> SendTemplateAsync(
		Guid leadId,
		SendAdmissionCrmMessageTemplateDto input)
	{
		return await SendTemplateCoreAsync(leadId, input, "Manual", enforceUserScope: true);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageTemplates)]
	public async Task<AdmissionCrmMessageTemplatePreviewDto> PreviewTemplateAsync(
		Guid academicYearId,
		PreviewAdmissionCrmMessageTemplateDto input)
	{
		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(
			academicYearId,
			nameof(PreviewTemplateAsync));
		var template = await GetTemplateForAcademicYearAsync(
			input.TemplateId,
			academicYearId,
			nameof(PreviewTemplateAsync));
		AdmissionInquiryLead? lead = null;
		if (input.LeadId.HasValue)
		{
			lead = await GetLeadForCurrentTenantAsync(input.LeadId.Value, nameof(PreviewTemplateAsync));
			if (lead.AcademicYearId != academicYearId)
			{
				throw new BusinessException("AdmissionCrm:LeadAcademicYearMismatch")
					.WithData("LeadId", lead.Id)
					.WithData("AcademicYearId", academicYearId);
			}
		}

		var variables = BuildPreviewVariables(template, lead, input.Variables);
		var renderedSubject = RenderTemplate(template.Subject, variables);
		var renderedBody = RenderTemplate(template.Body, variables) ?? string.Empty;
		return new AdmissionCrmMessageTemplatePreviewDto
		{
			TemplateId = template.Id,
			LeadId = lead?.Id,
			Channel = template.Channel,
			RenderedSubject = renderedSubject,
			RenderedBody = renderedBody,
			ResolvedVariables = variables,
			UnresolvedVariables = FindUnresolvedVariables(renderedSubject, renderedBody)
		};
	}

	[AllowAnonymous]
	public async Task<AdmissionCrmMessageDeliveryLogDto> SendTemplateFromAutomationAsync(
		Guid leadId,
		SendAdmissionCrmMessageTemplateDto input)
	{
		return await SendTemplateCoreAsync(leadId, input, "Automation", enforceUserScope: false);
	}

	private async Task<AdmissionCrmMessageDeliveryLogDto> SendTemplateCoreAsync(
		Guid leadId,
		SendAdmissionCrmMessageTemplateDto input,
		string trigger,
		bool enforceUserScope)
	{
		var lead = await GetLeadForCurrentTenantAsync(
			leadId,
			nameof(SendTemplateAsync),
			enforceUserScope);
		var template = await GetTemplateForLeadAsync(
			input.TemplateId,
			lead,
			nameof(SendTemplateAsync),
			enforceUserScope);
		var recipient = ResolveRecipient(lead, template.Channel, input.RecipientOverride);
		var normalizedRecipient = AdmissionCrmCommunicationOptOut.NormalizeContact(template.Channel, recipient);
		var provider = await GetProviderOrDefaultAsync(lead.AcademicYearId, template.Channel);
		var optOut = await GetActiveOptOutOrDefaultAsync(lead.AcademicYearId, template.Channel, normalizedRecipient);
		var now = Clock.Now;
		var systemVariables = BuildSystemVariables(lead);
		var providedVariables = input.Variables ?? new Dictionary<string, string>();
		var mergedVariables = systemVariables
			.Concat(providedVariables)
			.GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
			.ToDictionary(x => x.Key, x => x.Last().Value, StringComparer.OrdinalIgnoreCase);
		var subject = RenderTemplate(template.Subject, mergedVariables);
		var body = RenderTemplate(template.Body, mergedVariables);
		var dispatch = await ResolveDispatchResultAsync(
			provider,
			optOut,
			new AdmissionCrmCommunicationDispatchRequest
			{
				AcademicYearId = lead.AcademicYearId,
				LeadId = lead.Id,
				TemplateId = template.Id,
				Channel = template.Channel,
				Recipient = recipient,
				Subject = subject,
				Body = body ?? string.Empty,
				Trigger = trigger
			});

		var log = new AdmissionCrmMessageDeliveryLog(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			lead.AcademicYearId,
			lead.Id,
			template.Id,
			template.Channel,
			recipient,
			provider?.ProviderName,
			subject,
			body ?? string.Empty,
			dispatch.Status,
			dispatch.StatusDetails,
			now);
		log.RecordProviderMessageId(dispatch.ProviderMessageId);
		await _deliveryLogRepository.InsertAsync(log, autoSave: false);
		await AddTimelineEventAsync(lead, log, dispatch.StatusDetails, autoSave: true);
		return ToDto(log);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageTemplates)]
	public async Task<PagedResultDto<AdmissionCrmMessageDeliveryLogDto>> GetDeliveryLogsAsync(
		Guid academicYearId,
		GetAdmissionCrmMessageDeliveryLogListInput input)
	{
		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(
			academicYearId,
			nameof(GetDeliveryLogsAsync));
		var hasAcademicYearWideScope =
			await _studentScopeAccessService.CurrentUserHasAcademicYearWideScopeAsync(academicYearId);
		var scopedLeadIds = await GetScopedLeadIdsAsync(academicYearId);

		var queryable = await _deliveryLogRepository.GetQueryableAsync();
		var query = queryable.Where(x =>
			x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == academicYearId &&
			(hasAcademicYearWideScope ||
			 (x.LeadId.HasValue && scopedLeadIds.Contains(x.LeadId.Value))));

		if (input.LeadId.HasValue)
		{
			query = query.Where(x => x.LeadId == input.LeadId.Value);
		}

		if (input.TemplateId.HasValue)
		{
			query = query.Where(x => x.TemplateId == input.TemplateId.Value);
		}

		if (input.Channel.HasValue)
		{
			query = query.Where(x => x.Channel == input.Channel.Value);
		}

		if (!string.IsNullOrWhiteSpace(input.Status))
		{
			query = query.Where(x => x.Status == input.Status);
		}

		if (!string.IsNullOrWhiteSpace(input.ProviderMessageId))
		{
			var providerMessageId = input.ProviderMessageId.Trim();
			query = query.Where(x =>
				x.ProviderMessageId != null &&
				x.ProviderMessageId.Contains(providerMessageId));
		}

		var totalCount = await AsyncExecuter.CountAsync(query);
		var logs = await AsyncExecuter.ToListAsync(query
			.OrderByDescending(x => x.CreatedAt)
			.Skip(input.SkipCount)
			.Take(input.MaxResultCount));

		return new PagedResultDto<AdmissionCrmMessageDeliveryLogDto>(
			totalCount,
			logs.Select(ToDto).ToList());
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageTemplates)]
	public async Task<AdmissionCrmMessageDeliveryLogDto> RetryDeliveryAsync(
		Guid deliveryLogId,
		RetryAdmissionCrmMessageDeliveryDto input)
	{
		var log = await GetDeliveryLogForCurrentTenantAsync(deliveryLogId, nameof(RetryDeliveryAsync));
		var provider = await GetProviderOrDefaultAsync(log.AcademicYearId, log.Channel);
		if (provider is not { IsEnabled: true, Status: AdmissionCrmCommunicationProviderConfiguration.ConfiguredStatus })
		{
			throw new BusinessException("AdmissionCrm:ProviderNotConfigured")
				.WithData("Channel", log.Channel);
		}

		var optOut = await GetActiveOptOutOrDefaultAsync(log.AcademicYearId, log.Channel, log.NormalizedRecipient);
		if (optOut is not null)
		{
			throw new BusinessException("AdmissionCrm:RecipientOptedOut")
				.WithData("Channel", log.Channel)
				.WithData("Recipient", log.Recipient);
		}

		var dispatch = await _dispatcher.DispatchAsync(
			provider,
			new AdmissionCrmCommunicationDispatchRequest
			{
				AcademicYearId = log.AcademicYearId,
				LeadId = log.LeadId,
				TemplateId = log.TemplateId,
				Channel = log.Channel,
				Recipient = log.Recipient,
				Subject = log.SubjectSnapshot,
				Body = log.BodySnapshot,
				Trigger = string.IsNullOrWhiteSpace(input.Reason) ? "Retry" : input.Reason!
			});
		log.ApplyDispatchResult(dispatch.Status, dispatch.StatusDetails, Clock.Now, dispatch.ProviderMessageId);
		await _deliveryLogRepository.UpdateAsync(log, autoSave: true);
		return ToDto(log);
	}

	private async Task<IReadOnlyList<AdmissionCrmCommunicationProviderConfiguration>> GetOrCreateProvidersAsync(
		Guid academicYearId)
	{
		var providers = new List<AdmissionCrmCommunicationProviderConfiguration>();
		foreach (var channel in Enum.GetValues<AdmissionCrmMessageChannel>())
		{
			providers.Add(await GetOrCreateProviderAsync(academicYearId, channel));
		}

		return providers
			.OrderBy(x => x.Channel)
			.ToList();
	}

	private async Task<AdmissionCrmCommunicationProviderConfiguration> GetOrCreateProviderAsync(
		Guid academicYearId,
		AdmissionCrmMessageChannel channel)
	{
		var existing = await GetProviderOrDefaultAsync(academicYearId, channel);
		if (existing is not null)
		{
			return existing;
		}

		var provider = new AdmissionCrmCommunicationProviderConfiguration(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			academicYearId,
			channel);
		await _providerRepository.InsertAsync(provider, autoSave: true);
		return provider;
	}

	private async Task<AdmissionCrmCommunicationProviderConfiguration?> GetProviderOrDefaultAsync(
		Guid academicYearId,
		AdmissionCrmMessageChannel channel)
	{
		var queryable = await _providerRepository.GetQueryableAsync();
		return await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == academicYearId &&
			x.Channel == channel));
	}

	private async Task<AdmissionCrmCommunicationOptOut?> GetOptOutOrDefaultAsync(
		Guid academicYearId,
		AdmissionCrmMessageChannel channel,
		string normalizedContact)
	{
		var queryable = await _optOutRepository.GetQueryableAsync();
		return await AsyncExecuter.FirstOrDefaultAsync(queryable
			.Where(x =>
				x.TenantId == CurrentTenant.Id &&
				x.AcademicYearId == academicYearId &&
				x.Channel == channel &&
				x.NormalizedContactValue == normalizedContact)
			.OrderByDescending(x => x.IsActive)
			.ThenByDescending(x => x.OptedOutAt));
	}

	private async Task<AdmissionCrmCommunicationOptOut?> GetActiveOptOutOrDefaultAsync(
		Guid academicYearId,
		AdmissionCrmMessageChannel channel,
		string normalizedContact)
	{
		var queryable = await _optOutRepository.GetQueryableAsync();
		return await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == academicYearId &&
			x.Channel == channel &&
			x.NormalizedContactValue == normalizedContact &&
			x.IsActive));
	}

	private async Task<AdmissionInquiryLead> GetLeadForCurrentTenantAsync(
		Guid id,
		string operation,
		bool enforceUserScope = true)
	{
		var lead = await _leadRepository.GetAsync(id);
		if (lead.TenantId != CurrentTenant.Id)
		{
			throw new BusinessException("AdmissionCrm:LeadNotFound")
				.WithData("LeadId", id);
		}

		if (enforceUserScope)
		{
			await _studentScopeAccessService.EnsureCanAccessAdmissionLeadAsync(lead, operation);
		}

		return lead;
	}

	private async Task<AdmissionCrmMessageTemplate> GetTemplateForLeadAsync(
		Guid templateId,
		AdmissionInquiryLead lead,
		string operation,
		bool enforceUserScope = true)
	{
		var template = await GetTemplateForAcademicYearAsync(
			templateId,
			lead.AcademicYearId,
			operation,
			enforceUserScope);

		if (!template.IsActive || template.Status != AdmissionCrmMessageTemplateStatus.Approved)
		{
			throw new BusinessException("AdmissionCrm:MessageTemplateNotApproved")
				.WithData("TemplateId", templateId);
		}

		return template;
	}

	private async Task<AdmissionCrmMessageTemplate> GetTemplateForAcademicYearAsync(
		Guid templateId,
		Guid academicYearId,
		string operation,
		bool enforceUserScope = true)
	{
		var template = await _templateRepository.GetAsync(templateId);
		if (template.TenantId != CurrentTenant.Id || template.AcademicYearId != academicYearId)
		{
			throw new BusinessException("AdmissionCrm:MessageTemplateNotFound")
				.WithData("TemplateId", templateId);
		}

		if (enforceUserScope)
		{
			await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(template.AcademicYearId, operation);
		}

		return template;
	}

	private async Task<AdmissionCrmMessageDeliveryLog> GetDeliveryLogForCurrentTenantAsync(
		Guid deliveryLogId,
		string operation)
	{
		var log = await _deliveryLogRepository.GetAsync(deliveryLogId);
		if (log.TenantId != CurrentTenant.Id)
		{
			throw new BusinessException("AdmissionCrm:MessageDeliveryLogNotFound")
				.WithData("DeliveryLogId", deliveryLogId);
		}

		await EnsureLeadBelongsToAcademicYearAsync(log.LeadId, log.AcademicYearId, operation);
		return log;
	}

	private async Task EnsureLeadBelongsToAcademicYearAsync(Guid? leadId, Guid academicYearId, string operation)
	{
		if (!leadId.HasValue)
		{
			if (await _studentScopeAccessService.CurrentUserHasAcademicYearWideScopeAsync(academicYearId))
			{
				return;
			}

			throw new AbpAuthorizationException("An unassigned communication record requires academic-year-wide scope.");
		}

		var lead = await GetLeadForCurrentTenantAsync(leadId.Value, operation);
		if (lead.AcademicYearId != academicYearId)
		{
			throw new BusinessException("AdmissionCrm:LeadAcademicYearMismatch")
				.WithData("LeadId", leadId.Value)
				.WithData("AcademicYearId", academicYearId);
		}
	}

	private async Task<List<Guid>> GetScopedLeadIdsAsync(Guid academicYearId)
	{
		var queryable = await _leadRepository.GetQueryableAsync();
		var query = queryable.Where(x =>
			x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == academicYearId);
		query = await _studentScopeAccessService.ApplyCurrentUserAdmissionLeadScopeAsync(query);
		return await AsyncExecuter.ToListAsync(query.Select(x => x.Id));
	}

	private async Task AddTimelineEventAsync(
		AdmissionInquiryLead lead,
		AdmissionCrmMessageDeliveryLog log,
		string details,
		bool autoSave)
	{
		await _timelineRepository.InsertAsync(
			new AdmissionCrmTimelineEvent(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				lead.Id,
				lead.AcademicYearId,
				AdmissionCrmTimelineEventType.MessageTemplateChanged,
				"Message delivery recorded",
				$"{log.Channel}: {details}",
				log.Id,
				CurrentUser.Id,
				new DateTimeOffset(Clock.Now)),
			autoSave);
	}

	private static string ResolveRecipient(
		AdmissionInquiryLead lead,
		AdmissionCrmMessageChannel channel,
		string? recipientOverride)
	{
		if (!string.IsNullOrWhiteSpace(recipientOverride))
		{
			return recipientOverride.Trim();
		}

		var recipient = channel == AdmissionCrmMessageChannel.Email
			? lead.Email
			: lead.PrimaryContactNo;

		if (string.IsNullOrWhiteSpace(recipient))
		{
			throw new BusinessException("AdmissionCrm:MessageRecipientMissing")
				.WithData("LeadId", lead.Id)
				.WithData("Channel", channel);
		}

		return recipient;
	}

	private async Task<AdmissionCrmCommunicationDispatchResult> ResolveDispatchResultAsync(
		AdmissionCrmCommunicationProviderConfiguration? provider,
		AdmissionCrmCommunicationOptOut? optOut,
		AdmissionCrmCommunicationDispatchRequest request)
	{
		if (optOut is not null)
		{
			return new AdmissionCrmCommunicationDispatchResult
			{
				Status = AdmissionCrmMessageDeliveryLog.BlockedByOptOutStatus,
				StatusDetails = "Delivery blocked because the recipient opted out."
			};
		}

		if (provider is not { IsEnabled: true, Status: AdmissionCrmCommunicationProviderConfiguration.ConfiguredStatus })
		{
			return new AdmissionCrmCommunicationDispatchResult
			{
				Status = AdmissionCrmMessageDeliveryLog.ProviderNotConfiguredStatus,
				StatusDetails = "Delivery logged but not sent because no enabled provider is configured."
			};
		}

		return await _dispatcher.DispatchAsync(provider, request);
	}

	private static Dictionary<string, string> BuildSystemVariables(AdmissionInquiryLead lead)
	{
		var studentName = string.Join(
			" ",
			new[] { lead.FirstName, lead.MiddleName, lead.LastName }.Where(x => !string.IsNullOrWhiteSpace(x)));

		return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			["studentName"] = string.IsNullOrWhiteSpace(studentName) ? "Applicant" : studentName,
			["parentName"] = lead.ParentGuardianName ?? "Parent",
			["admissionIntent"] = lead.AdmissionIntent,
			["desiredClass"] = lead.DesiredClassName ?? string.Empty,
			["campusBranch"] = lead.CampusBranchName ?? string.Empty,
			["preferredLanguage"] = lead.PreferredCommunicationLanguage ?? string.Empty,
			["primaryContactNo"] = lead.PrimaryContactNo ?? string.Empty,
			["email"] = lead.Email ?? string.Empty,
			["leadScore"] = lead.LeadScore.ToString()
		};
	}

	private static Dictionary<string, string> BuildPreviewVariables(
		AdmissionCrmMessageTemplate template,
		AdmissionInquiryLead? lead,
		Dictionary<string, string>? providedVariables)
	{
		var variables = lead is null
			? BuildSampleVariables()
			: BuildSystemVariables(lead);

		foreach (var sampleVariable in BuildSampleVariables())
		{
			variables.TryAdd(sampleVariable.Key, sampleVariable.Value);
		}

		foreach (var variable in DeserializeVariables(template.VariablesJson))
		{
			variables.TryAdd(variable, $"{variable} sample");
		}

		foreach (var variable in providedVariables ?? [])
		{
			variables[variable.Key] = variable.Value;
		}

		return variables;
	}

	private static Dictionary<string, string> BuildSampleVariables()
	{
		return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			["studentName"] = "Sample Student",
			["parentName"] = "Sample Parent",
			["schoolName"] = "Sample School",
			["admissionIntent"] = "Admission inquiry for Grade 1",
			["desiredClass"] = "Grade 1",
			["primaryContactNo"] = "9999999999",
			["email"] = "parent@example.com",
			["leadScore"] = "50",
			["examDate"] = "15 July",
			["examTime"] = "10:00 AM",
			["room"] = "101",
			["statusLink"] = "https://school.example/status",
			["ownerName"] = "Admissions Office",
			["nextStepInstructions"] = "Please visit the school reception."
		};
	}

	private static string? RenderTemplate(string? template, IReadOnlyDictionary<string, string> variables)
	{
		if (string.IsNullOrWhiteSpace(template))
		{
			return template;
		}

		var rendered = template;
		foreach (var variable in variables)
		{
			rendered = rendered.Replace(
				"{{" + variable.Key + "}}",
				variable.Value,
				StringComparison.OrdinalIgnoreCase);
		}

		return rendered;
	}

	private static IReadOnlyList<string> DeserializeVariables(string variablesJson)
	{
		return JsonSerializer.Deserialize<List<string>>(variablesJson) ?? [];
	}

	private static IReadOnlyList<string> FindUnresolvedVariables(params string?[] renderedValues)
	{
		return renderedValues
			.Where(value => !string.IsNullOrWhiteSpace(value))
			.SelectMany(value => Regex.Matches(value!, "\\{\\{\\s*(?<name>[^{}]+?)\\s*\\}\\}").Cast<Match>())
			.Select(match => match.Groups["name"].Value.Trim())
			.Where(name => !string.IsNullOrWhiteSpace(name))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.OrderBy(name => name)
			.ToList();
	}

	private static AdmissionCrmCommunicationProviderDto ToDto(
		AdmissionCrmCommunicationProviderConfiguration provider)
	{
		return new AdmissionCrmCommunicationProviderDto
		{
			Id = provider.Id,
			AcademicYearId = provider.AcademicYearId,
			Channel = provider.Channel,
			Status = provider.Status,
			IsEnabled = provider.IsEnabled,
			ProviderName = provider.ProviderName,
			SenderId = provider.SenderId,
			Settings = SanitizeProviderSettings(
				DeserializeProviderSettings(provider.SettingsJson)),
			WebhookUrl = provider.WebhookUrl,
			LastValidatedAt = provider.LastValidatedAt
		};
	}

	private static string SerializeProviderSettings(AdmissionCrmCommunicationProviderSettingsDto settings)
	{
		settings ??= new AdmissionCrmCommunicationProviderSettingsDto();
		var adapter = (settings.Adapter ?? string.Empty).Trim().ToLowerInvariant();
		if (adapter is not ("manual" or "manual-queue" or "webhook" or "generic-webhook" or "custom-webhook"))
			throw new BusinessException("AdmissionCrm:UnsupportedCommunicationAdapter").WithData("Adapter", settings.Adapter);
		var payloadMode = (settings.PayloadMode ?? string.Empty).Trim().ToLowerInvariant();
		if (payloadMode is not ("default" or "envelope"))
			throw new BusinessException("AdmissionCrm:UnsupportedCommunicationPayloadMode").WithData("PayloadMode", settings.PayloadMode);
		if (settings.Headers.Count > 20 || settings.Parameters.Count > 20 ||
		    settings.Headers.Any(item => item.Key.Length > 120 || item.Value.Length > 1000) ||
		    settings.Parameters.Any(item => item.Key.Length > 120 || item.Value.Length > 1000))
			throw new BusinessException("AdmissionCrm:CommunicationProviderSettingsTooLarge");
		settings.Adapter = adapter;
		settings.PayloadMode = payloadMode;
		return JsonSerializer.Serialize(settings, JsonOptions);
	}

	internal static AdmissionCrmCommunicationProviderSettingsDto MergeProviderSettings(
		AdmissionCrmCommunicationProviderSettingsDto stored,
		AdmissionCrmCommunicationProviderSettingsDto? input)
	{
		input ??= new AdmissionCrmCommunicationProviderSettingsDto();
		var headers = MergeWriteOnlyValues(stored.Headers, input.Headers);
		var parameters = MergeWriteOnlyValues(stored.Parameters, input.Parameters);
		return new AdmissionCrmCommunicationProviderSettingsDto
		{
			Adapter = input.Adapter,
			PayloadMode = input.PayloadMode,
			ResponseProviderMessageIdPath = input.ResponseProviderMessageIdPath,
			Headers = headers,
			Parameters = parameters,
			CallbackSecret = string.IsNullOrWhiteSpace(input.CallbackSecret)
				? stored.CallbackSecret
				: input.CallbackSecret.Trim(),
			CallbackMappings = input.CallbackMappings ??
				new AdmissionCrmCommunicationCallbackMappingsDto()
		};
	}

	private static Dictionary<string, string> MergeWriteOnlyValues(
		IReadOnlyDictionary<string, string> stored,
		IReadOnlyDictionary<string, string>? input)
	{
		var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (var (key, value) in input ?? new Dictionary<string, string>())
		{
			var normalizedKey = key.Trim();
			if (normalizedKey.Length == 0) continue;
			result[normalizedKey] = string.IsNullOrWhiteSpace(value) &&
				stored.TryGetValue(normalizedKey, out var storedValue)
					? storedValue
					: value;
		}

		return result;
	}

	internal static AdmissionCrmCommunicationProviderSettingsDto SanitizeProviderSettings(
		AdmissionCrmCommunicationProviderSettingsDto settings)
	{
		return new AdmissionCrmCommunicationProviderSettingsDto
		{
			Adapter = settings.Adapter,
			PayloadMode = settings.PayloadMode,
			ResponseProviderMessageIdPath = settings.ResponseProviderMessageIdPath,
			Headers = settings.Headers.ToDictionary(
				item => item.Key,
				_ => string.Empty,
				StringComparer.OrdinalIgnoreCase),
			Parameters = settings.Parameters.ToDictionary(
				item => item.Key,
				_ => string.Empty,
				StringComparer.OrdinalIgnoreCase),
			CallbackSecret = null,
			HasStoredHeaderValues = settings.Headers.Values.Any(value =>
				!string.IsNullOrWhiteSpace(value)),
			HasStoredParameterValues = settings.Parameters.Values.Any(value =>
				!string.IsNullOrWhiteSpace(value)),
			HasCallbackSecret = !string.IsNullOrWhiteSpace(settings.CallbackSecret),
			CallbackMappings = settings.CallbackMappings
		};
	}

	private static void ValidateProviderEndpoint(
		AdmissionCrmCommunicationProviderSettingsDto settings,
		string? webhookUrl)
	{
		var adapter = (settings?.Adapter ?? string.Empty).Trim().ToLowerInvariant();
		if (adapter is not ("webhook" or "generic-webhook" or "custom-webhook")) return;
		if (!Uri.TryCreate(webhookUrl, UriKind.Absolute, out var uri) ||
		    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
			throw new BusinessException("AdmissionCrm:InvalidCommunicationWebhookUrl")
				.WithData("WebhookUrl", webhookUrl);
	}

	private static AdmissionCrmCommunicationProviderSettingsDto DeserializeProviderSettings(string? settingsJson)
	{
		if (string.IsNullOrWhiteSpace(settingsJson)) return new AdmissionCrmCommunicationProviderSettingsDto();
		try
		{
			return JsonSerializer.Deserialize<AdmissionCrmCommunicationProviderSettingsDto>(settingsJson, JsonOptions)
			       ?? new AdmissionCrmCommunicationProviderSettingsDto();
		}
		catch (JsonException)
		{
			return new AdmissionCrmCommunicationProviderSettingsDto { Adapter = "unsupported-json" };
		}
	}

	private static AdmissionCrmCommunicationOptOutDto ToDto(AdmissionCrmCommunicationOptOut optOut)
	{
		return new AdmissionCrmCommunicationOptOutDto
		{
			Id = optOut.Id,
			AcademicYearId = optOut.AcademicYearId,
			LeadId = optOut.LeadId,
			Channel = optOut.Channel,
			ContactValue = optOut.ContactValue,
			NormalizedContactValue = optOut.NormalizedContactValue,
			IsActive = optOut.IsActive,
			Reason = optOut.Reason,
			OptedOutAt = optOut.OptedOutAt,
			OptedInAt = optOut.OptedInAt,
			OptInReason = optOut.OptInReason
		};
	}

	private static AdmissionCrmMessageDeliveryLogDto ToDto(AdmissionCrmMessageDeliveryLog log)
	{
		return new AdmissionCrmMessageDeliveryLogDto
		{
			Id = log.Id,
			AcademicYearId = log.AcademicYearId,
			LeadId = log.LeadId,
			TemplateId = log.TemplateId,
			Channel = log.Channel,
			Recipient = log.Recipient,
			NormalizedRecipient = log.NormalizedRecipient,
			ProviderName = log.ProviderName,
			ProviderMessageId = log.ProviderMessageId,
			SubjectSnapshot = log.SubjectSnapshot,
			BodySnapshot = log.BodySnapshot,
			Status = log.Status,
			StatusDetails = log.StatusDetails,
			AttemptCount = log.AttemptCount,
			CreatedAt = log.CreatedAt,
			LastAttemptAt = log.LastAttemptAt,
			SentAt = log.SentAt,
			DeliveredAt = log.DeliveredAt
		};
	}
}
