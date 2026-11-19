using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShikshaMitra.Communication;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Application.Communication;

public partial class CommunicationAppService
{
	private const string ParentRegistrationSource = "AdmissionParentRegistration";
	internal async Task<string?> ParentRegistrationDeliveryBlockedReasonAsync()
	{
		var provider = await ParentRegistrationEmailProviderAsync();
		if (provider == null) return "The Email provider is disabled or unavailable. Review Email in Communication settings.";
		return _channelGateways.Any(gateway => gateway is not QueueOnlyCommunicationChannelGateway && gateway.CanHandle(CommunicationChannelType.Email, provider.ProviderName))
			? null : "The configured Email provider cannot send invitations. Review Email in Communication settings.";
	}

	private async Task<ChannelProviderSetting?> ParentRegistrationEmailProviderAsync()
	{
		var configured = await GetEnabledProvidersAsync([CommunicationChannelType.Email]);
		if (configured.TryGetValue(CommunicationChannelType.Email, out var provider)) return provider;
		// An explicitly configured (including disabled, suspended or removed) channel
		// always retains its school's policy. Never reactivate it through the fallback.
		using (LazyServiceProvider.LazyGetRequiredService<IDataFilter>().Disable<ISoftDelete>())
			if (await _providerSettingRepository.AnyAsync(row => row.Channel == CommunicationChannelType.Email)) return null;
		// This explicit, saved-owner transactional invitation uses the same ABP sender
		// as Student Onboarding. No second SMTP configuration or provider row is created.
		// Explicit withdrawal and central campaign approval are still checked at publish.
		return new ChannelProviderSetting(Guid.Empty, CurrentTenant.Id)
		{
			Channel = CommunicationChannelType.Email, ProviderName = "SMTP", IsEnabled = true,
			Status = CommunicationProviderStatus.Enabled, ConsentRequired = false
		};
	}
	internal async Task<Guid> PrepareParentRegistrationAsync(AdmissionParentRegistration grant, AdmissionApplication application,
		AdmissionCycle cycle, AdmissionParentRecipientDto recipient)
	{
		var previous = grant.CampaignId.HasValue ? await _campaignRepository.FindAsync(grant.CampaignId.Value) : null;
		if (previous != null)
		{
			if (!previous.PublishedAt.HasValue) return previous.Id;
			var attempts = await _deliveryAttemptRepository.GetListAsync(row => row.CampaignId == previous.Id);
			// Skips and explicit SMTP rejections did not accept the message. Retain that
			// history in a new campaign only after staff explicitly retries. Timeouts,
			// queued/accepted and unclassified failures are never retried automatically.
			if (attempts.Count == 0 || attempts.Any(row => !ParentRegistrationAttemptCanRetry(row))) return previous.Id;
		}
		var campaign = await CreateCampaignCoreAsync(new CreateUpdateCommunicationCampaignDto
		{
			AcademicYearId = cycle.AcademicYearId, SourceModule = CommunicationSourceModule.Student,
			SourceEntityType = ParentRegistrationSource, SourceEntityId = grant.Id, SourceReference = application.ApplicationNumber,
			CampaignType = "Notification", Title = "Parent registration: " + application.ApplicationNumber,
			Subject = "Complete your school registration - " + application.ApplicationNumber,
			Body = ParentRegistrationEmailContent.PlainText(),
			Channels = [CommunicationChannelType.Email], FallbackOrder = [CommunicationChannelType.Email],
			AudienceDefinition = new CommunicationAudienceDefinitionDto { ExplicitRecipients = [new() { RecipientType = "AdmissionInquiry",
				RecipientId = application.ApplicantId, DisplayName = recipient.FullName, Email = recipient.Email, IsParentDefault = true }] }
		});
		return campaign.Id;
	}
	internal async Task<ParentRegistrationDeliveryResult> PublishParentRegistrationAsync(Guid campaignId, AdmissionParentRegistration grant,
		AdmissionApplication application, AdmissionParentRecipientDto recipient, string privateLink)
	{
		var campaign = await _campaignRepository.GetAsync(campaignId);
		if (campaign.SourceEntityType != ParentRegistrationSource || campaign.SourceEntityId != grant.Id || campaign.PublishedAt.HasValue || await HasCampaignHistoryAsync(campaignId))
			return new("Unknown", "Delivery acceptance could not be confirmed. Review delivery history before sending a new link.");
		var audience = Deserialize(campaign.AudienceDefinitionJson, new CommunicationAudienceDefinitionDto());
		ValidateParentRegistrationAudience(audience, Deserialize(campaign.ChannelsJson, new List<CommunicationChannelType>()), application.ApplicantId, recipient);
		await PublishCampaignCoreAsync(campaign, privateLink);
		var attempts = await _deliveryAttemptRepository.GetListAsync(row => row.CampaignId == campaignId);
		if (attempts.Count > 0 && attempts.All(row => row.Status is CommunicationDeliveryStatus.Sent or CommunicationDeliveryStatus.Delivered)) return new("Sent");
		if (attempts.Count > 0 && attempts.All(ParentRegistrationAttemptCanRetry))
			return new("Failed", ParentRegistrationDeliveryError(attempts.FirstOrDefault(row => row.ErrorCode != null)?.ErrorCode));
		return new("Unknown", "Delivery acceptance could not be confirmed. Review delivery history before sending a new link.");
	}
	private static bool ParentRegistrationAttemptCanRetry(DeliveryAttempt attempt) => attempt.Status == CommunicationDeliveryStatus.Skipped ||
		attempt.Status == CommunicationDeliveryStatus.Failed && attempt.ErrorCode == "SMTP_SEND_FAILED";

	internal static string ParentRegistrationDeliveryError(string? code) => code switch
	{
		"Communication:ApprovalRequired" => "Communication approval is required before sending.",
		"Communication:ProviderMissing" => "The Email provider is disabled or unavailable. Review Email in Communication settings.",
		"Communication:GatewayMissing" => "The configured Email provider cannot send invitations. Review Email in Communication settings.",
		"Communication:ClientUrlMissing" => "The school's registration page address is missing. Review the application settings.",
		"Communication:TemplateRequired" or "Communication:TemplateApprovalRequired" => "The Email provider requires an approved template. Review Communication template settings.",
		"CONSENT_REQUIRED" => "The parent has no email opt-in consent. Record consent in Communication before sending.",
		"CONSENT_WITHDRAWN" => "The parent withdrew email consent. Review Communication consent before sending another invitation.",
		"SMTP_SEND_FAILED" => "The school's email sender rejected this invitation. Review the email settings used by Student Onboarding, then try again.",
		_ => "The saved recipient, staff authority or communication setup is unavailable. Review the saved contact and delivery history."
	};
	internal static void ValidateParentRegistrationAudience(CommunicationAudienceDefinitionDto audience, List<CommunicationChannelType> channels,
		Guid applicantId, AdmissionParentRecipientDto recipient)
	{
		if (audience.ExplicitRecipients.Count != 1 || channels.Count != 1 || channels[0] != CommunicationChannelType.Email ||
			audience.GroupIds.Count + audience.OrganizationIds.Count + audience.ClassIds.Count + audience.DivisionIds.Count + audience.StudentIds.Count +
			audience.ParentGuardianIds.Count + audience.EmployeeIds.Count + audience.UserIds.Count + audience.Roles.Count + audience.Filters.Count != 0 ||
			audience.IncludeStudentsWhenEligible)
			throw new BusinessException("Communication:RecipientChanged");
		var target = audience.ExplicitRecipients[0];
		if (target.RecipientType != "AdmissionInquiry" || target.RecipientId != applicantId || target.Email != recipient.Email || target.UserId.HasValue ||
			!string.IsNullOrWhiteSpace(target.MobileNumber) || target.DisplayName != recipient.FullName)
			throw new BusinessException("Communication:RecipientChanged");
	}
}

internal sealed record ParentRegistrationDeliveryResult(string Status, string? Error = null);
