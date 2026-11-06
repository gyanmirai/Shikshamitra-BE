using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Application.Communication;
using ShikshaMitra.Communication;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionParentRegistrationAppService
{
	private const string ParentRegistrationCampaignSource = "AdmissionParentRegistration";

	public async Task<List<AdmissionParentEmailHistoryDto>> GetEmailHistoryAsync(Guid applicationId)
	{
		var application = await access.StaffAsync(applicationId, false);
		var grants = (await data.Repo<AdmissionParentRegistration>().GetListAsync(row => row.ApplicationId == applicationId))
			.OrderByDescending(row => row.CreationTime).ThenByDescending(row => row.Id).ToList();
		if (grants.Count == 0) return [];

		var grantIds = grants.Select(row => row.Id).ToArray();
		var campaigns = await data.Repo<CommunicationCampaign>().GetListAsync(row =>
			row.SourceEntityType == ParentRegistrationCampaignSource && row.SourceEntityId.HasValue && grantIds.Contains(row.SourceEntityId.Value));
		var campaignIds = campaigns.Select(row => row.Id).ToArray();
		List<DeliveryAttempt> attempts = campaignIds.Length == 0 ? [] : await data.Repo<DeliveryAttempt>().GetListAsync(row => campaignIds.Contains(row.CampaignId));
		var applicant = await data.Repo<AdmissionApplicant>().GetAsync(application.ApplicantId);
		var recipients = AdmissionParentRegistrationData.Recipients(applicant);
		var initial = await data.IsInitialAllowedAsync(application);
		var open = AdmissionParentRegistrationData.IsOpen(application);
		var history = new List<AdmissionParentEmailHistoryDto>(grants.Count);
		foreach (var grant in grants)
		{
			var recipient = JsonSerializer.Deserialize<AdmissionParentRecipientDto>(grant.RecipientJson)!;
			var linkStatus = grant.Status;
			if (grant.CanRead(Clock.Now))
			{
				var savedRecipient = recipients.FirstOrDefault(item => item.Relationship == recipient.Relationship && item.CanSend);
				var stageAllowed = grant.Purpose == "initial" ? initial : application.SubmittedAt.HasValue
					? application.CanCorrectForm : initial || await data.IsDocumentCorrectionAllowedAsync(application, grant);
				if (stageAllowed) stageAllowed = await data.AreDocumentCorrectionStagesOpenAsync(application, grant);
				if (!open || !stageAllowed || savedRecipient == null ||
					AdmissionParentRegistrationData.RecipientFingerprint(applicant, savedRecipient) != grant.RecipientFingerprint)
					linkStatus = "Closed";
			}
			else if (grant.ExpiresAt <= Clock.Now && grant.Status is "Invited" or "InProgress")
				linkStatus = "Expired";

			var grantCampaigns = campaigns.Where(row => row.SourceEntityId == grant.Id).OrderBy(row => row.CreationTime).ToList();
			var historyAttempts = grantCampaigns.SelectMany(campaign => attempts.Where(attempt => attempt.CampaignId == campaign.Id)
				.Select(attempt => new AdmissionParentEmailAttemptDto
				{
					Status = attempt.Status.ToString(),
					AttemptNumber = attempt.AttemptNumber,
					AttemptedAt = attempt.SentAt ?? attempt.QueuedAt ?? attempt.DeliveredAt ?? campaign.PublishedAt,
					Destination = attempt.Destination,
					SafeMessage = attempt.ErrorCode == null ? null : CommunicationAppService.ParentRegistrationDeliveryError(attempt.ErrorCode)
				})).ToList();
			if (historyAttempts.Count == 0)
				historyAttempts.Add(new AdmissionParentEmailAttemptDto
				{
					Status = grant.DeliveryStatus,
					AttemptedAt = grant.DeliveryStartedAt.HasValue
						? new DateTimeOffset(DateTime.SpecifyKind(grant.DeliveryStartedAt.Value, DateTimeKind.Utc))
						: new DateTimeOffset(DateTime.SpecifyKind(grant.CreationTime, DateTimeKind.Utc)),
					Destination = recipient.Email,
					SafeMessage = grant.DeliveryError
				});
			history.Add(new AdmissionParentEmailHistoryDto
			{
				RegistrationId = grant.Id,
				Purpose = grant.Purpose,
				LinkStatus = linkStatus,
				RecipientName = recipient.FullName,
				RecipientRelationship = recipient.Relationship,
				RecipientRelationshipLabel = recipient.RelationshipLabel,
				RecipientEmail = recipient.Email,
				ExpiresAt = grant.ExpiresAt,
				LastSavedAt = grant.LastSavedAt,
				HandedInAt = grant.HandedInAt,
				Notes = grant.Notes,
				Attempts = historyAttempts.OrderByDescending(item => item.AttemptedAt).ToList()
			});
		}
		return history;
	}
}

