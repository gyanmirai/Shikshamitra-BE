using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Communication;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Application.Communication;

public partial class CommunicationAppService
{
	private const string InquiryQualificationSource = "AdmissionInquiryQualification";

	private async Task<bool> HasWithdrawnInquiryConsentAsync(CommunicationRecipientSnapshot recipient,
		CommunicationChannelType channel, string destination)
	{
		var query = await _consentRecordRepository.GetQueryableAsync();
		return await AsyncExecuter.AnyAsync(query.Where(item => item.Channel == channel &&
			item.Destination == destination && item.RecipientId == recipient.RecipientId &&
			(item.Status == CommunicationConsentStatus.OptedOut || item.Status == CommunicationConsentStatus.Revoked)));
	}

	internal async Task RecordPublicInquiryConsentAsync(AdmissionInquiry inquiry, Guid linkId)
	{
		if (string.IsNullOrWhiteSpace(inquiry.Email)) return;
		var recipientId = inquiry.ApplicantId ?? inquiry.Id;
		var consent = await _consentRecordRepository.FindAsync(item => item.RecipientId == recipientId &&
			item.Channel == CommunicationChannelType.Email && item.Destination == inquiry.Email);
		if (consent != null) return;
		await _consentRecordRepository.InsertAsync(new ConsentRecord(GuidGenerator.Create(), CurrentTenant.Id)
		{
			RecipientType = "AdmissionInquiry", RecipientId = recipientId,
			DisplayName = inquiry.ApplicantName, Channel = CommunicationChannelType.Email,
			Destination = inquiry.Email, Status = CommunicationConsentStatus.OptedIn,
			Source = "PublicAdmissionInquiry", ConsentCapturedAt = DateTimeOffset.UtcNow,
			Notes = "Guardian consent to inquiry contact captured through public link " + linkId
		}, autoSave: false);
	}

	// Internal system notification, never a staff/public API. Approval, consent and provider checks
	// use the same Communication implementation as manually-created campaigns.
	internal async Task<CommunicationCampaignDto> PrepareInquiryQualificationAsync(AdmissionInquiry inquiry, AdmissionCycle cycle, AdmissionSchoolContactDto? mainContact = null)
	{
		var existing = await _campaignRepository.FirstOrDefaultAsync(item =>
			item.SourceEntityType == InquiryQualificationSource && item.SourceEntityId == inquiry.Id);
		if (existing != null) return MapCampaign(existing);
		return await CreateCampaignCoreAsync(new CreateUpdateCommunicationCampaignDto
		{
			AcademicYearId = cycle.AcademicYearId,
			SourceModule = CommunicationSourceModule.Student,
			SourceEntityType = InquiryQualificationSource, SourceEntityId = inquiry.Id,
			SourceReference = inquiry.InquiryNumber, CampaignType = "Notification",
			Title = "Admission inquiry interested: " + inquiry.InquiryNumber,
			Subject = "Your admission inquiry - " + inquiry.InquiryNumber,
			Body = "Thank you for your interest in admission for " + inquiry.ApplicantName +
				". Your inquiry " + inquiry.InquiryNumber + " is marked Interested. Our admission counsellor will guide you through the next steps.",
			Channels = [CommunicationChannelType.Email], FallbackOrder = [CommunicationChannelType.Email],
			LanguageCode = inquiry.PreferredLanguageCode,
			AudienceDefinition = new CommunicationAudienceDefinitionDto
			{
				ExplicitRecipients = [new CommunicationExplicitRecipientDto
				{
					RecipientType = "AdmissionInquiry", RecipientId = inquiry.ApplicantId ?? inquiry.Id,
					DisplayName = mainContact?.FullName ?? inquiry.ApplicantName, Email = mainContact != null ? mainContact.Email : inquiry.Email,
					PreferredLanguageCode = inquiry.PreferredLanguageCode, IsParentDefault = true
				}]
			}
		});
	}

	internal async Task<CommunicationCampaignDto> PublishInquiryQualificationAsync(Guid campaignId, Guid inquiryId)
	{
		var campaign = await _campaignRepository.GetAsync(campaignId);
		if (campaign.SourceEntityType != InquiryQualificationSource || campaign.SourceEntityId != inquiryId)
			throw new InvalidOperationException("The notification does not belong to this inquiry.");
		if (campaign.PublishedAt.HasValue || await HasCampaignHistoryAsync(campaignId) ||
			campaign.Status is CommunicationCampaignStatus.PendingApproval or CommunicationCampaignStatus.Cancelled)
			return MapCampaign(campaign);
		return await PublishCampaignCoreAsync(campaign);
	}
}
