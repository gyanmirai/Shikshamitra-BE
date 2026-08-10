using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmMessageDeliveryWorkflow(
	IRepository<AdmissionCrmMessageDeliveryLog, Guid> deliveryLogRepository,
	IRepository<AdmissionCrmMessageTemplate, Guid> templateRepository,
	IRepository<AdmissionCrmTimelineEvent, Guid> timelineRepository,
	AdmissionCrmCommunicationAccess access,
	AdmissionCrmCommunicationProviderWorkflow providerWorkflow,
	AdmissionCrmCommunicationOptOutWorkflow optOutWorkflow,
	IAdmissionCrmCommunicationDispatcher dispatcher)
	: ApplicationService, ITransientDependency
{
	public Task<AdmissionCrmMessageDeliveryLogDto> SendAsync(Guid leadId, SendAdmissionCrmMessageTemplateDto input) =>
		SendCoreAsync(leadId, input, "Manual", enforceUserScope: true);

	public Task<AdmissionCrmMessageDeliveryLogDto> SendFromAutomationAsync(Guid leadId, SendAdmissionCrmMessageTemplateDto input) =>
		SendCoreAsync(leadId, input, "Automation", enforceUserScope: false);

	public async Task<AdmissionCrmMessageTemplatePreviewDto> PreviewAsync(Guid academicYearId, PreviewAdmissionCrmMessageTemplateDto input)
	{
		await access.EnsureYearAsync(academicYearId, "PreviewTemplateAsync");
		var template = await GetTemplateForYearAsync(input.TemplateId, academicYearId, "PreviewTemplateAsync");
		AdmissionInquiryLead? lead = null;
		if (input.LeadId.HasValue)
		{
			lead = await access.GetLeadAsync(input.LeadId.Value, "PreviewTemplateAsync");
			if (lead.AcademicYearId != academicYearId)
				throw new BusinessException("AdmissionCrm:LeadAcademicYearMismatch")
					.WithData("LeadId", lead.Id).WithData("AcademicYearId", academicYearId);
		}
		var variables = AdmissionCrmMessageTemplateRenderer.PreviewVariables(template, lead, input.Variables);
		var subject = AdmissionCrmMessageTemplateRenderer.Render(template.Subject, variables);
		var body = AdmissionCrmMessageTemplateRenderer.Render(template.Body, variables) ?? string.Empty;
		return new AdmissionCrmMessageTemplatePreviewDto
		{
			TemplateId = template.Id, LeadId = lead?.Id, Channel = template.Channel, RenderedSubject = subject,
			RenderedBody = body, ResolvedVariables = variables,
			UnresolvedVariables = AdmissionCrmMessageTemplateRenderer.FindUnresolved(subject, body)
		};
	}

	public async Task<PagedResultDto<AdmissionCrmMessageDeliveryLogDto>> GetLogsAsync(
		Guid academicYearId,
		GetAdmissionCrmMessageDeliveryLogListInput input)
	{
		await access.EnsureYearAsync(academicYearId, "GetDeliveryLogsAsync");
		var hasWideScope = await access.HasYearWideScopeAsync(academicYearId);
		var scopedLeadIds = await access.GetScopedLeadIdsAsync(academicYearId);
		var queryable = await deliveryLogRepository.GetQueryableAsync();
		var query = queryable.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId &&
			(hasWideScope || (x.LeadId.HasValue && scopedLeadIds.Contains(x.LeadId.Value))));
		if (input.LeadId.HasValue) query = query.Where(x => x.LeadId == input.LeadId.Value);
		if (input.TemplateId.HasValue) query = query.Where(x => x.TemplateId == input.TemplateId.Value);
		if (input.Channel.HasValue) query = query.Where(x => x.Channel == input.Channel.Value);
		if (!string.IsNullOrWhiteSpace(input.Status)) query = query.Where(x => x.Status == input.Status);
		if (!string.IsNullOrWhiteSpace(input.ProviderMessageId))
		{
			var providerMessageId = input.ProviderMessageId.Trim();
			query = query.Where(x => x.ProviderMessageId != null && x.ProviderMessageId.Contains(providerMessageId));
		}
		var totalCount = await AsyncExecuter.CountAsync(query);
		var logs = await AsyncExecuter.ToListAsync(query.OrderByDescending(x => x.CreatedAt)
			.Skip(input.SkipCount).Take(input.MaxResultCount));
		return new PagedResultDto<AdmissionCrmMessageDeliveryLogDto>(totalCount,
			logs.Select(AdmissionCrmCommunicationMapper.Delivery).ToList());
	}

	public async Task<AdmissionCrmMessageDeliveryLogDto> RetryAsync(Guid deliveryLogId, RetryAdmissionCrmMessageDeliveryDto input)
	{
		var log = await deliveryLogRepository.GetAsync(deliveryLogId);
		if (log.TenantId != CurrentTenant.Id)
			throw new BusinessException("AdmissionCrm:MessageDeliveryLogNotFound").WithData("DeliveryLogId", deliveryLogId);
		await access.EnsureLeadBelongsToYearAsync(log.LeadId, log.AcademicYearId, "RetryDeliveryAsync");
		var provider = await providerWorkflow.FindAsync(log.AcademicYearId, log.Channel);
		if (provider is not { IsEnabled: true, Status: AdmissionCrmCommunicationProviderConfiguration.ConfiguredStatus })
			throw new BusinessException("AdmissionCrm:ProviderNotConfigured").WithData("Channel", log.Channel);
		var optOut = await optOutWorkflow.FindActiveAsync(log.AcademicYearId, log.Channel, log.NormalizedRecipient);
		if (optOut is not null)
			throw new BusinessException("AdmissionCrm:RecipientOptedOut").WithData("Channel", log.Channel).WithData("Recipient", log.Recipient);
		var dispatch = await dispatcher.DispatchAsync(provider, new AdmissionCrmCommunicationDispatchRequest
		{
			AcademicYearId = log.AcademicYearId, LeadId = log.LeadId, TemplateId = log.TemplateId, Channel = log.Channel,
			Recipient = log.Recipient, Subject = log.SubjectSnapshot, Body = log.BodySnapshot,
			Trigger = string.IsNullOrWhiteSpace(input.Reason) ? "Retry" : input.Reason!
		});
		log.ApplyDispatchResult(dispatch.Status, dispatch.StatusDetails, Clock.Now, dispatch.ProviderMessageId);
		await deliveryLogRepository.UpdateAsync(log, autoSave: true);
		return AdmissionCrmCommunicationMapper.Delivery(log);
	}

	private async Task<AdmissionCrmMessageDeliveryLogDto> SendCoreAsync(
		Guid leadId,
		SendAdmissionCrmMessageTemplateDto input,
		string trigger,
		bool enforceUserScope)
	{
		var lead = await access.GetLeadAsync(leadId, "SendTemplateAsync", enforceUserScope);
		var template = await GetTemplateForLeadAsync(input.TemplateId, lead, enforceUserScope);
		var recipient = AdmissionCrmMessageTemplateRenderer.ResolveRecipient(lead, template.Channel, input.RecipientOverride);
		var normalizedRecipient = AdmissionCrmCommunicationOptOut.NormalizeContact(template.Channel, recipient);
		var provider = await providerWorkflow.FindAsync(lead.AcademicYearId, template.Channel);
		var optOut = await optOutWorkflow.FindActiveAsync(lead.AcademicYearId, template.Channel, normalizedRecipient);
		var variables = AdmissionCrmMessageTemplateRenderer.SystemVariables(lead)
			.Concat(input.Variables ?? new Dictionary<string, string>()).GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
			.ToDictionary(x => x.Key, x => x.Last().Value, StringComparer.OrdinalIgnoreCase);
		var subject = AdmissionCrmMessageTemplateRenderer.Render(template.Subject, variables);
		var body = AdmissionCrmMessageTemplateRenderer.Render(template.Body, variables) ?? string.Empty;
		var dispatch = await ResolveDispatchAsync(provider, optOut, new AdmissionCrmCommunicationDispatchRequest
		{
			AcademicYearId = lead.AcademicYearId, LeadId = lead.Id, TemplateId = template.Id, Channel = template.Channel,
			Recipient = recipient, Subject = subject, Body = body, Trigger = trigger
		});
		var log = new AdmissionCrmMessageDeliveryLog(GuidGenerator.Create(), CurrentTenant.Id, lead.AcademicYearId,
			lead.Id, template.Id, template.Channel, recipient, provider?.ProviderName, subject, body,
			dispatch.Status, dispatch.StatusDetails, Clock.Now);
		log.RecordProviderMessageId(dispatch.ProviderMessageId);
		await deliveryLogRepository.InsertAsync(log, autoSave: false);
		await timelineRepository.InsertAsync(new AdmissionCrmTimelineEvent(GuidGenerator.Create(), CurrentTenant.Id, lead.Id,
			lead.AcademicYearId, AdmissionCrmTimelineEventType.MessageTemplateChanged, "Message delivery recorded",
			$"{log.Channel}: {dispatch.StatusDetails}", log.Id, CurrentUser.Id, new DateTimeOffset(Clock.Now)), autoSave: true);
		return AdmissionCrmCommunicationMapper.Delivery(log);
	}

	private async Task<AdmissionCrmMessageTemplate> GetTemplateForLeadAsync(Guid templateId, AdmissionInquiryLead lead, bool enforceUserScope)
	{
		var template = await GetTemplateForYearAsync(templateId, lead.AcademicYearId, "SendTemplateAsync", enforceUserScope);
		if (!template.IsActive || template.Status != AdmissionCrmMessageTemplateStatus.Approved)
			throw new BusinessException("AdmissionCrm:MessageTemplateNotApproved").WithData("TemplateId", templateId);
		return template;
	}

	private async Task<AdmissionCrmMessageTemplate> GetTemplateForYearAsync(
		Guid templateId, Guid academicYearId, string operation, bool enforceUserScope = true)
	{
		var template = await templateRepository.GetAsync(templateId);
		if (template.TenantId != CurrentTenant.Id || template.AcademicYearId != academicYearId)
			throw new BusinessException("AdmissionCrm:MessageTemplateNotFound").WithData("TemplateId", templateId);
		if (enforceUserScope) await access.EnsureYearAsync(template.AcademicYearId, operation);
		return template;
	}

	private Task<AdmissionCrmCommunicationDispatchResult> ResolveDispatchAsync(
		AdmissionCrmCommunicationProviderConfiguration? provider,
		AdmissionCrmCommunicationOptOut? optOut,
		AdmissionCrmCommunicationDispatchRequest request)
	{
		if (optOut is not null) return Task.FromResult(new AdmissionCrmCommunicationDispatchResult
			{ Status = AdmissionCrmMessageDeliveryLog.BlockedByOptOutStatus, StatusDetails = "Delivery blocked because the recipient opted out." });
		if (provider is not { IsEnabled: true, Status: AdmissionCrmCommunicationProviderConfiguration.ConfiguredStatus })
			return Task.FromResult(new AdmissionCrmCommunicationDispatchResult
			{ Status = AdmissionCrmMessageDeliveryLog.ProviderNotConfiguredStatus, StatusDetails = "Delivery logged but not sent because no enabled provider is configured." });
		return dispatcher.DispatchAsync(provider, request);
	}
}
