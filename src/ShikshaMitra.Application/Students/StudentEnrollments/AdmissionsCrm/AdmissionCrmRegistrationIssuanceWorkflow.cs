using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmRegistrationIssuanceWorkflow(
	IRepository<AdmissionCrmRegistrationApplication, Guid> applicationRepository,
	IRepository<AdmissionInquiryLead, Guid> leadRepository,
	AdmissionCrmRegistrationAccess access,
	AdmissionCrmRegistrationEligibilityWorkflow eligibility,
	AdmissionCrmRegistrationFormNumberWorkflow formNumbers,
	AdmissionCrmApplicantStatusLinkWorkflow statusLinks,
	AdmissionCrmRegistrationTimeline timeline,
	AdmissionCrmDocumentChecklistWorkflow documentChecklist,
	AdmissionCrmMessageDeliveryWorkflow messageDelivery,
	IConfiguration configuration) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmRegistrationIssuanceResultDto> IssueAsync(
		Guid leadId,
		IssueAdmissionCrmRegistrationApplicationDto input)
	{
		var provenance = input.AccessMode == AdmissionCrmRegistrationAccessMode.Online
			? AdmissionCrmRegistrationProvenance.OnlineParent
			: AdmissionCrmRegistrationProvenance.StaffAssistedCounter;
		var registration = await IssueCoreAsync(leadId, input, false, provenance, input.AccessMode);
		var result = new AdmissionCrmRegistrationIssuanceResultDto { Registration = registration };
		if (input.AccessMode == AdmissionCrmRegistrationAccessMode.StaffAssisted) return result;
		var lead = await access.GetLeadAsync(leadId, "SendRegistrationInvitationAsync");
		return await DeliverInvitationAsync(lead, result);
	}

	public async Task<AdmissionCrmRegistrationIssuanceResultDto> SendInvitationAsync(Guid registrationId)
	{
		var application = await access.GetApplicationAsync(registrationId, "SendRegistrationInvitationAsync");
		var lead = await access.GetLeadAsync(application.LeadId, "SendRegistrationInvitationAsync");
		var registration = AdmissionCrmRegistrationModelMapper.ToApplicationDto(application);
		registration.ApplicantStatusPath = await statusLinks.EnsureAsync(lead, Clock.Now.AddDays(90));
		return await DeliverInvitationAsync(lead, new AdmissionCrmRegistrationIssuanceResultDto
		{
			Registration = registration
		});
	}

	private async Task<AdmissionCrmRegistrationIssuanceResultDto> DeliverInvitationAsync(
		AdmissionInquiryLead lead,
		AdmissionCrmRegistrationIssuanceResultDto result)
	{
		try
		{
			if (string.IsNullOrWhiteSpace(result.Registration.ApplicantStatusPath))
				throw new BusinessException("AdmissionCrm:ApplicantStatusLinkUnavailable");
			var clientUrl = configuration["App:ClientUrl"]?.TrimEnd('/');
			if (string.IsNullOrWhiteSpace(clientUrl))
				throw new BusinessException("AdmissionCrm:ClientUrlNotConfigured");
			var link = clientUrl + "/" + result.Registration.ApplicantStatusPath.TrimStart('/');
			result.Delivery = await messageDelivery.SendRegistrationInvitationAsync(lead.Id, link);
		}
		catch (BusinessException exception)
		{
			result.DeliveryError = exception.Code ?? exception.Message;
			await timeline.AddAsync(lead, AdmissionCrmTimelineEventType.RegistrationInvitationSent,
				"Registration invitation not sent", result.DeliveryError, result.Registration.Id);
		}
		return result;
	}

	public Task<AdmissionCrmRegistrationApplicationDto> IssueDirectAsync(Guid leadId, IssueAdmissionCrmRegistrationApplicationDto input) =>
		IssueCoreAsync(leadId, input, true, AdmissionCrmRegistrationProvenance.StaffAssistedCounter,
			AdmissionCrmRegistrationAccessMode.StaffAssisted);

	public async Task<AdmissionCrmRegistrationApplicationDto> IssueFromPaperAsync(Guid leadId, CreatePaperAdmissionCrmRegistrationDto input)
	{
		var dto = await IssueCoreAsync(leadId, input.Registration, true, AdmissionCrmRegistrationProvenance.StaffEnteredFromPaper,
			AdmissionCrmRegistrationAccessMode.StaffAssisted);
		var application = await access.GetApplicationAsync(dto.Id, "IssueFromPaperAsync");
		application.RecordPaperReceipt(input.PhysicalFormNumber,
			input.PhysicalFormReceivedDate == default ? Clock.Now.Date : input.PhysicalFormReceivedDate,
			input.PhysicalFormReceivedByUserId ?? CurrentUser.Id ?? throw new BusinessException("AdmissionCrm:PaperRegistrationRequiresStaffUser"),
			input.ScannedFormAttachmentReference);
		await applicationRepository.UpdateAsync(application, autoSave: true);
		await timeline.AddForApplicationAsync(application, AdmissionCrmTimelineEventType.PaperRegistrationEntered,
			"Paper registration entered", $"Physical form {application.PhysicalFormNumber} received on {application.PhysicalFormReceivedDate:yyyy-MM-dd}.");
		return AdmissionCrmRegistrationModelMapper.ToApplicationDto(application);
	}

	public async Task EnsureStandardEligibleAsync(Guid leadId, Guid? playbookId = null)
	{
		var lead = await access.GetLeadAsync(leadId, "EnsureStandardIssuanceEligibleAsync");
		await eligibility.EnsureAsync(lead, await eligibility.ResolvePathAsync(lead, playbookId), false);
	}

	private async Task<AdmissionCrmRegistrationApplicationDto> IssueCoreAsync(Guid leadId,
		IssueAdmissionCrmRegistrationApplicationDto input, bool direct, AdmissionCrmRegistrationProvenance provenance,
		AdmissionCrmRegistrationAccessMode accessMode)
	{
		if (input.WaiveFee)
			await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.WaiveRegistrationFee);
		var lead = await access.GetLeadAsync(leadId, "IssueAsync");
		var template = await access.GetTemplateAsync(input.RegistrationTemplateId, "IssueAsync");
		if (template.AcademicYearId != lead.AcademicYearId || template.Status != AdmissionCrmRegistrationTemplateStatus.Published)
			throw new BusinessException("AdmissionCrm:PublishedRegistrationTemplateRequired");
		var path = direct && !input.PlaybookId.HasValue
			? AdmissionCrmRegistrationPath.DirectRegistration : await eligibility.ResolvePathAsync(lead, input.PlaybookId);
		await eligibility.EnsureAsync(lead, path, direct);
		var queryable = await applicationRepository.GetQueryableAsync();
		var existing = await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.LeadId == leadId && x.Status != AdmissionCrmRegistrationStatus.Revoked));
		if (existing != null) throw new BusinessException("AdmissionCrm:RegistrationApplicationAlreadyIssued").WithData("ApplicationId", existing.Id);
		var formNumber = await formNumbers.ResolveAsync(lead, input);
		await formNumbers.EnsureUniqueAsync(formNumber, lead.AcademicYearId);
		var expiryDays = input.LinkExpiryDays ?? template.LinkExpiryDays;
		if (expiryDays is < 1 or > 365) throw new BusinessException("AdmissionCrm:InvalidRegistrationLinkExpiryDays");
		var now = Clock.Now;
		var application = new AdmissionCrmRegistrationApplication(GuidGenerator.Create(), CurrentTenant.Id, lead.AcademicYearId,
			lead.Id, template.Id, template.Version, input.PlaybookId, formNumber,
			AdmissionCrmRegistrationModelMapper.Serialize(AdmissionCrmRegistrationModelMapper.ToTemplateDto(template)),
			AdmissionCrmRegistrationModelMapper.Serialize(AdmissionCrmRegistrationModelMapper.BuildPrefill(lead)), now,
			now.AddDays(expiryDays), template.FeeRequired ? template.FeeAmount : 0, provenance, accessMode);
		if (input.WaiveFee) application.WaiveFee(Check.NotNullOrWhiteSpace(input.WaiverReason, nameof(input.WaiverReason), AdmissionCrmConsts.MaxNotesLength));
		await applicationRepository.InsertAsync(application, autoSave: false);
		var statusPath = await statusLinks.EnsureAsync(lead, now.AddDays(Math.Max(expiryDays, 90)));
		if (!lead.RegistrationFormIssuedAt.HasValue)
		{
			lead.IssueRegistrationForm(formNumber, decimal.ToDouble(template.FeeAmount), now);
			await leadRepository.UpdateAsync(lead, autoSave: false);
		}
		await timeline.AddAsync(lead, AdmissionCrmTimelineEventType.RegistrationFormIssued, "Registration form issued",
			$"Form {formNumber}, template {template.Name} v{template.Version}, access {accessMode}.", application.Id);
		await CurrentUnitOfWork!.SaveChangesAsync();
		await documentChecklist.AssignApplicableChecklistFromAutomationAsync(application.Id,
			new AdmissionCrmPlaybookApplicabilityDto { Campus = lead.CampusBranchName, DesiredClass = lead.DesiredClassName });
		var dto = AdmissionCrmRegistrationModelMapper.ToApplicationDto(application);
		dto.ApplicantStatusPath = statusPath;
		return dto;
	}
}
