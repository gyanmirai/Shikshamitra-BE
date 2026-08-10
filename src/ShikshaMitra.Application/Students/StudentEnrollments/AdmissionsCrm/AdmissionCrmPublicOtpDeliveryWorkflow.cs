using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public sealed record AdmissionCrmPublicOtpDeliveryResult(bool DeliveryConfigured, string DeliveryStatus);
internal sealed record AdmissionCrmPublicOtpDeliveryCandidate(AdmissionCrmMessageChannel Channel, string Recipient);

public class AdmissionCrmPublicOtpDeliveryWorkflow(
	IRepository<AdmissionInquiryConsentRecord, Guid> consentRepository,
	IRepository<AdmissionCrmCommunicationProviderConfiguration, Guid> providerRepository,
	IRepository<AdmissionCrmCommunicationOptOut, Guid> optOutRepository,
	IRepository<AdmissionCrmMessageDeliveryLog, Guid> deliveryLogRepository,
	IRepository<AdmissionCrmTimelineEvent, Guid> timelineRepository,
	IAdmissionCrmCommunicationDispatcher dispatcher) : ApplicationService, ITransientDependency
{
	private const int OtpExpiryMinutes = 10;

	public async Task<AdmissionCrmPublicOtpDeliveryResult> DispatchAsync(
		AdmissionInquiryLead lead,
		string otpCode,
		DateTime issuedAt,
		string? verifiedGuardianContact = null)
	{
		var candidates = !string.IsNullOrWhiteSpace(verifiedGuardianContact)
			? new List<AdmissionCrmPublicOtpDeliveryCandidate>
			{
				new(
					verifiedGuardianContact.Contains('@')
						? AdmissionCrmMessageChannel.Email
						: AdmissionCrmMessageChannel.Sms,
					verifiedGuardianContact.Trim())
			}
			: BuildCandidates(lead, await GetConsentedChannelsAsync(lead.Id)).ToList();
		if (candidates.Count == 0)
		{
			return new(false, "OTP challenge created. No consented contact channel is available for delivery.");
		}

		var selected = candidates[0];
		var selectedProvider = await GetProviderAsync(lead.AcademicYearId, selected.Channel);
		foreach (var candidate in candidates)
		{
			var provider = await GetProviderAsync(lead.AcademicYearId, candidate.Channel);
			if (IsConfigured(provider))
			{
				selected = candidate;
				selectedProvider = provider;
				break;
			}
		}

		var normalizedRecipient = AdmissionCrmCommunicationOptOut.NormalizeContact(
			selected.Channel, selected.Recipient);
		var optOut = await GetActiveOptOutAsync(lead.AcademicYearId, selected.Channel, normalizedRecipient);
		var dispatch = await ResolveDispatchAsync(selectedProvider, optOut, lead, selected, otpCode);
		var log = new AdmissionCrmMessageDeliveryLog(
			GuidGenerator.Create(), CurrentTenant.Id, lead.AcademicYearId, lead.Id, templateId: null,
			selected.Channel, selected.Recipient, selectedProvider?.ProviderName,
			selected.Channel == AdmissionCrmMessageChannel.Email ? "Admission status OTP" : null,
			$"Your admission status OTP is ******. It expires in {OtpExpiryMinutes} minutes.",
			dispatch.Status, dispatch.StatusDetails, issuedAt);
		log.RecordProviderMessageId(dispatch.ProviderMessageId);
		await deliveryLogRepository.InsertAsync(log, autoSave: false);
		await timelineRepository.InsertAsync(
			new AdmissionCrmTimelineEvent(
				GuidGenerator.Create(), CurrentTenant.Id, lead.Id, lead.AcademicYearId,
				AdmissionCrmTimelineEventType.MessageTemplateChanged, "Status OTP delivery recorded",
				$"{selected.Channel}: {dispatch.StatusDetails}", log.Id, actorUserId: null,
				occurredAt: new DateTimeOffset(issuedAt)),
			autoSave: false);
		return new(IsConfigured(selectedProvider), dispatch.StatusDetails);
	}

	private async Task<HashSet<AdmissionCrmMessageChannel>> GetConsentedChannelsAsync(Guid leadId)
	{
		var queryable = await consentRepository.GetQueryableAsync();
		var consent = await AsyncExecuter.FirstOrDefaultAsync(queryable
			.Where(item => item.TenantId == CurrentTenant.Id && item.LeadId == leadId)
			.OrderByDescending(item => item.AcceptedAt));
		if (consent is null)
		{
			return Enum.GetValues<AdmissionCrmMessageChannel>().ToHashSet();
		}

		var preferences = JsonSerializer.Deserialize<List<string>>(
			consent.CommunicationPreferencesJson, AdmissionCrmPublicFormat.JsonOptions) ?? [];
		var channels = new HashSet<AdmissionCrmMessageChannel>();
		foreach (var preference in preferences)
		{
			if (preference.Contains("sms", StringComparison.OrdinalIgnoreCase)) channels.Add(AdmissionCrmMessageChannel.Sms);
			if (preference.Contains("whatsapp", StringComparison.OrdinalIgnoreCase)) channels.Add(AdmissionCrmMessageChannel.WhatsApp);
			if (preference.Contains("email", StringComparison.OrdinalIgnoreCase)) channels.Add(AdmissionCrmMessageChannel.Email);
		}

		return channels;
	}

	private async Task<AdmissionCrmCommunicationProviderConfiguration?> GetProviderAsync(
		Guid academicYearId,
		AdmissionCrmMessageChannel channel)
	{
		var queryable = await providerRepository.GetQueryableAsync();
		return await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(provider =>
			provider.TenantId == CurrentTenant.Id &&
			provider.AcademicYearId == academicYearId &&
			provider.Channel == channel));
	}

	private async Task<AdmissionCrmCommunicationOptOut?> GetActiveOptOutAsync(
		Guid academicYearId,
		AdmissionCrmMessageChannel channel,
		string normalizedContact)
	{
		var queryable = await optOutRepository.GetQueryableAsync();
		return await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(optOut =>
			optOut.TenantId == CurrentTenant.Id && optOut.AcademicYearId == academicYearId &&
			optOut.Channel == channel && optOut.NormalizedContactValue == normalizedContact && optOut.IsActive));
	}

	private async Task<AdmissionCrmCommunicationDispatchResult> ResolveDispatchAsync(
		AdmissionCrmCommunicationProviderConfiguration? provider,
		AdmissionCrmCommunicationOptOut? optOut,
		AdmissionInquiryLead lead,
		AdmissionCrmPublicOtpDeliveryCandidate candidate,
		string otpCode)
	{
		if (optOut is not null)
		{
			return new AdmissionCrmCommunicationDispatchResult
			{
				Status = AdmissionCrmMessageDeliveryLog.BlockedByOptOutStatus,
				StatusDetails = "OTP delivery blocked because the recipient opted out."
			};
		}

		if (!IsConfigured(provider))
		{
			return new AdmissionCrmCommunicationDispatchResult
			{
				Status = AdmissionCrmMessageDeliveryLog.ProviderNotConfiguredStatus,
				StatusDetails = "OTP challenge created but not sent because no enabled provider is configured for the consented channel."
			};
		}

		return await dispatcher.DispatchAsync(
			provider!,
			new AdmissionCrmCommunicationDispatchRequest
			{
				AcademicYearId = lead.AcademicYearId,
				LeadId = lead.Id,
				Channel = candidate.Channel,
				Recipient = candidate.Recipient,
				Subject = candidate.Channel == AdmissionCrmMessageChannel.Email ? "Admission status OTP" : null,
				Body = $"Your admission status OTP is {otpCode}. It expires in {OtpExpiryMinutes} minutes.",
				Trigger = "StatusOtp"
			});
	}

	private static bool IsConfigured(AdmissionCrmCommunicationProviderConfiguration? provider) =>
		provider is { IsEnabled: true, Status: AdmissionCrmCommunicationProviderConfiguration.ConfiguredStatus };

	private static IEnumerable<AdmissionCrmPublicOtpDeliveryCandidate> BuildCandidates(
		AdmissionInquiryLead lead,
		IReadOnlySet<AdmissionCrmMessageChannel> consentedChannels)
	{
		if (!string.IsNullOrWhiteSpace(lead.PrimaryContactNo))
		{
			if (consentedChannels.Contains(AdmissionCrmMessageChannel.Sms))
				yield return new(AdmissionCrmMessageChannel.Sms, lead.PrimaryContactNo);
			if (consentedChannels.Contains(AdmissionCrmMessageChannel.WhatsApp))
				yield return new(AdmissionCrmMessageChannel.WhatsApp, lead.PrimaryContactNo);
		}

		if (!string.IsNullOrWhiteSpace(lead.Email) && consentedChannels.Contains(AdmissionCrmMessageChannel.Email))
		{
			yield return new(AdmissionCrmMessageChannel.Email, lead.Email);
		}
	}
}
