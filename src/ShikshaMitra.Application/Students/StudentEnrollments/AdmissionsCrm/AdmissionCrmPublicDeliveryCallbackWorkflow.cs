using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmPublicDeliveryCallbackWorkflow(
	IRepository<AdmissionCrmMessageDeliveryLog, Guid> logRepository,
	IRepository<AdmissionCrmCommunicationProviderConfiguration, Guid> providerRepository,
	IRepository<AdmissionCrmTimelineEvent, Guid> timelineRepository) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmMessageDeliveryLogDto> ApplyAsync(
		Guid tenantId, Guid deliveryLogId, ApplyAdmissionCrmDeliveryCallbackDto input)
	{
		using (CurrentTenant.Change(tenantId))
		{
			var log = await logRepository.GetAsync(deliveryLogId);
			if (log.TenantId != CurrentTenant.Id)
				throw new BusinessException("AdmissionCrm:MessageDeliveryLogNotFound").WithData("DeliveryLogId", deliveryLogId);
			var provider = await GetProviderAsync(log.AcademicYearId, log.Channel);
			var callback = AdmissionCrmDeliveryCallbackParser.Resolve(provider?.SettingsJson, input);
			AdmissionCrmDeliveryCallbackParser.EnsureSecret(provider?.SettingsJson, callback, log.Channel, deliveryLogId);
			return await ApplyResolvedAsync(log, callback);
		}
	}

	public async Task<AdmissionCrmMessageDeliveryLogDto> ApplyByProviderMessageAsync(
		Guid tenantId,
		Guid academicYearId,
		AdmissionCrmMessageChannel channel,
		ApplyAdmissionCrmDeliveryCallbackDto input)
	{
		using (CurrentTenant.Change(tenantId))
		{
			var provider = await GetProviderAsync(academicYearId, channel);
			var callback = AdmissionCrmDeliveryCallbackParser.Resolve(provider?.SettingsJson, input);
			AdmissionCrmDeliveryCallbackParser.EnsureSecret(provider?.SettingsJson, callback, channel);
			var providerMessageId = AdmissionCrmDeliveryCallbackParser.NormalizeProviderMessageId(callback.ProviderMessageId);
			if (string.IsNullOrWhiteSpace(providerMessageId))
				throw new BusinessException("AdmissionCrm:DeliveryCallbackProviderMessageIdRequired")
					.WithData("AcademicYearId", academicYearId).WithData("Channel", channel);
			var log = await GetLogAsync(academicYearId, channel, providerMessageId);
			return await ApplyResolvedAsync(log, callback with { ProviderMessageId = providerMessageId });
		}
	}

	private async Task<AdmissionCrmCommunicationProviderConfiguration?> GetProviderAsync(
		Guid academicYearId, AdmissionCrmMessageChannel channel) =>
		await AsyncExecuter.FirstOrDefaultAsync((await providerRepository.GetQueryableAsync()).Where(item =>
			item.TenantId == CurrentTenant.Id && item.AcademicYearId == academicYearId && item.Channel == channel));

	private async Task<AdmissionCrmMessageDeliveryLog> GetLogAsync(
		Guid academicYearId, AdmissionCrmMessageChannel channel, string providerMessageId)
	{
		var matches = await AsyncExecuter.ToListAsync((await logRepository.GetQueryableAsync()).Where(item =>
			item.TenantId == CurrentTenant.Id && item.AcademicYearId == academicYearId && item.Channel == channel &&
			item.ProviderMessageId == providerMessageId).OrderByDescending(item => item.CreationTime).Take(2));
		if (matches.Count == 0)
			throw new BusinessException("AdmissionCrm:MessageDeliveryLogNotFound").WithData("AcademicYearId", academicYearId)
				.WithData("Channel", channel).WithData("ProviderMessageId", providerMessageId);
		if (matches.Count > 1)
			throw new BusinessException("AdmissionCrm:DeliveryCallbackProviderMessageIdAmbiguous").WithData("AcademicYearId", academicYearId)
				.WithData("Channel", channel).WithData("ProviderMessageId", providerMessageId);
		return matches[0];
	}

	private async Task<AdmissionCrmMessageDeliveryLogDto> ApplyResolvedAsync(
		AdmissionCrmMessageDeliveryLog log, AdmissionCrmResolvedDeliveryCallback callback)
	{
		var status = AdmissionCrmDeliveryCallbackParser.NormalizeStatus(callback.Status);
		var details = AdmissionCrmDeliveryCallbackParser.BuildDetails(status, callback.StatusDetails, callback.ProviderMessageId);
		var now = Clock.Now;
		var applied = log.ApplyProviderCallback(status, details, callback.ProviderMessageId, now);
		await logRepository.UpdateAsync(log, autoSave: false);
		if (log.LeadId.HasValue)
		{
			await timelineRepository.InsertAsync(
				new AdmissionCrmTimelineEvent(
					GuidGenerator.Create(), CurrentTenant.Id, log.LeadId.Value, log.AcademicYearId,
					AdmissionCrmTimelineEventType.MessageTemplateChanged,
					applied ? "Message delivery callback received" : "Message delivery callback ignored",
					applied ? $"{log.Channel}: {details}" :
						$"{log.Channel}: Ignored stale {status} callback because current delivery status is {log.Status}. {details}",
					log.Id, null, new DateTimeOffset(now)),
				autoSave: false);
		}
		await CurrentUnitOfWork!.SaveChangesAsync();
		return ToDto(log);
	}

	private static AdmissionCrmMessageDeliveryLogDto ToDto(AdmissionCrmMessageDeliveryLog log) => new()
	{
		Id = log.Id, AcademicYearId = log.AcademicYearId, LeadId = log.LeadId, TemplateId = log.TemplateId,
		Channel = log.Channel, Recipient = log.Recipient, NormalizedRecipient = log.NormalizedRecipient,
		ProviderName = log.ProviderName, ProviderMessageId = log.ProviderMessageId,
		SubjectSnapshot = log.SubjectSnapshot, BodySnapshot = log.BodySnapshot, Status = log.Status,
		StatusDetails = log.StatusDetails, AttemptCount = log.AttemptCount, CreatedAt = log.CreatedAt,
		LastAttemptAt = log.LastAttemptAt, SentAt = log.SentAt, DeliveredAt = log.DeliveredAt
	};
}
