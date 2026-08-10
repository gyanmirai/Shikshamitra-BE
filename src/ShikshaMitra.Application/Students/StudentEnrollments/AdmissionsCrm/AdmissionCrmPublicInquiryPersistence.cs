using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public sealed record AdmissionCrmPublicInquiryPersistenceInput(
	Guid TenantId,
	AdmissionInquiryLead Lead,
	AdmissionInquiryFormTemplate Template,
	PublicAdmissionInquirySubmitDto Submission,
	string SubmissionKey,
	string? IpAddress,
	string? UserAgent,
	int DuplicateCount,
	Guid? OwnerUserId,
	Guid? TrackedOwnerUserId);

public class AdmissionCrmPublicInquiryPersistence(
	IRepository<AdmissionInquiryLead, Guid> leadRepository,
	IRepository<AdmissionInquiryFormSubmission, Guid> submissionRepository,
	IRepository<AdmissionInquiryConsentRecord, Guid> consentRepository,
	IRepository<AdmissionApplicantStatusAccess, Guid> statusAccessRepository,
	IRepository<AdmissionCrmTimelineEvent, Guid> timelineRepository,
	AdmissionCrmPublicTokenService tokens) : ApplicationService, ITransientDependency
{
	private const int StatusTokenExpiryDays = 90;

	public async Task<PublicAdmissionInquirySubmitResultDto?> GetExistingAsync(
		Guid templateId,
		string submissionKey)
	{
		var submission = await submissionRepository.FirstOrDefaultAsync(item =>
			item.FormTemplateId == templateId && item.SubmissionKey == submissionKey.Trim());
		if (submission == null)
		{
			return null;
		}

		var lead = await leadRepository.GetAsync(submission.LeadId);
		var access = (await statusAccessRepository.GetListAsync(item => item.LeadId == lead.Id))
			.OrderByDescending(item => item.LinkIssuedAt)
			.FirstOrDefault();
		if (access?.EncryptedToken == null)
		{
			throw new BusinessException("AdmissionCrm:StatusAccessUnavailable");
		}

		var statusToken = tokens.DecryptRequired(access.EncryptedToken);
		return Result(lead, statusToken);
	}

	public async Task<PublicAdmissionInquirySubmitResultDto> PersistAsync(
		AdmissionCrmPublicInquiryPersistenceInput input)
	{
		var now = Clock.Now;
		await leadRepository.InsertAsync(input.Lead, autoSave: false);
		await submissionRepository.InsertAsync(
			new AdmissionInquiryFormSubmission(
				GuidGenerator.Create(), input.TenantId, input.Lead.Id, input.Template.Id,
				input.Template.TemplateName,
				JsonSerializer.Serialize(input.Submission.FieldValues, AdmissionCrmPublicFormat.JsonOptions),
				now, input.IpAddress, input.UserAgent, input.SubmissionKey),
			autoSave: false);
		await consentRepository.InsertAsync(
			new AdmissionInquiryConsentRecord(
				GuidGenerator.Create(), input.TenantId, input.Lead.Id, input.Template.Id,
				input.Template.TemplateName, input.Template.ConsentText!, input.Template.PrivacyNoticeUrl!,
				input.Template.ConcurrencyStamp,
				AdmissionCrmPublicSubmissionMapper.SerializeCommunicationPreferences(
					input.Submission.CommunicationPreferences),
				now, input.IpAddress, input.UserAgent),
			autoSave: false);

		var statusToken = tokens.CreateRawToken();
		var statusAccess = new AdmissionApplicantStatusAccess(
			GuidGenerator.Create(), input.TenantId, input.Lead.Id,
			AdmissionCrmPublicTokenService.HashSecret(statusToken), tokens.EncryptRequired(statusToken),
			now.AddDays(StatusTokenExpiryDays), now);
		await AddTimelineAsync(
			input, AdmissionCrmTimelineEventType.PublicFormSubmitted, "Public form submitted",
			input.Template.TemplateName, input.Template.Id);
		if (input.OwnerUserId.HasValue)
		{
			await AddTimelineAsync(
				input,
				AdmissionCrmTimelineEventType.OwnerAssigned,
				input.TrackedOwnerUserId.HasValue
					? "Owner assigned from staff-shared link"
					: "Owner assigned automatically",
				$"Owner user: {input.OwnerUserId}",
				input.OwnerUserId);
		}

		if (input.DuplicateCount > 0)
		{
			await AddTimelineAsync(
				input,
				AdmissionCrmTimelineEventType.DuplicateContinued,
				"Possible duplicate detected",
				"Public form matched an existing lead by contact, email, or applicant profile. No duplicate details were exposed to the applicant.",
				null);
		}

		await statusAccessRepository.InsertAsync(statusAccess, autoSave: true);
		return Result(input.Lead, statusToken);
	}

	private Task AddTimelineAsync(
		AdmissionCrmPublicInquiryPersistenceInput input,
		AdmissionCrmTimelineEventType eventType,
		string title,
		string details,
		Guid? relatedEntityId) =>
		timelineRepository.InsertAsync(
			new AdmissionCrmTimelineEvent(
				GuidGenerator.Create(), input.TenantId, input.Lead.Id, input.Lead.AcademicYearId,
				eventType, title, details, relatedEntityId, actorUserId: null,
				occurredAt: new DateTimeOffset(Clock.Now)),
			autoSave: false);

	private PublicAdmissionInquirySubmitResultDto Result(AdmissionInquiryLead lead, string statusToken) =>
		new()
		{
			LeadId = lead.Id,
			AcknowledgementMessage = "Inquiry submitted successfully.",
			StatusToken = statusToken,
			StatusPath = AdmissionCrmPublicTokenService.CreateStatusPath(CurrentTenant.Id!.Value, statusToken),
			Stage = lead.Stage
		};
}
