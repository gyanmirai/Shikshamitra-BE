using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
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
	AdmissionCrmDocumentChecklistWorkflow documentChecklist) : ApplicationService, ITransientDependency
{
	public Task<AdmissionCrmRegistrationApplicationDto> IssueAsync(Guid leadId, IssueAdmissionCrmRegistrationApplicationDto input) =>
		IssueCoreAsync(leadId, input, false, AdmissionCrmRegistrationProvenance.OnlineParent);

	public Task<AdmissionCrmRegistrationApplicationDto> IssueDirectAsync(Guid leadId, IssueAdmissionCrmRegistrationApplicationDto input) =>
		IssueCoreAsync(leadId, input, true, AdmissionCrmRegistrationProvenance.StaffAssistedCounter);

	public async Task<AdmissionCrmRegistrationApplicationDto> IssueFromPaperAsync(Guid leadId, CreatePaperAdmissionCrmRegistrationDto input)
	{
		var dto = await IssueCoreAsync(leadId, input.Registration, true, AdmissionCrmRegistrationProvenance.StaffEnteredFromPaper);
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
		IssueAdmissionCrmRegistrationApplicationDto input, bool direct, AdmissionCrmRegistrationProvenance provenance)
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
			now.AddDays(expiryDays), template.FeeRequired ? template.FeeAmount : 0, provenance);
		if (input.WaiveFee) application.WaiveFee(Check.NotNullOrWhiteSpace(input.WaiverReason, nameof(input.WaiverReason), AdmissionCrmConsts.MaxNotesLength));
		await applicationRepository.InsertAsync(application, autoSave: false);
		var statusPath = await statusLinks.EnsureAsync(lead, now.AddDays(Math.Max(expiryDays, 90)));
		if (!lead.RegistrationFormIssuedAt.HasValue)
		{
			lead.IssueRegistrationForm(formNumber, template.FeeAmount, now);
			await leadRepository.UpdateAsync(lead, autoSave: false);
		}
		await timeline.AddAsync(lead, AdmissionCrmTimelineEventType.RegistrationFormIssued, "Registration form issued",
			$"Form {formNumber}, template {template.Name} v{template.Version}.", application.Id);
		await CurrentUnitOfWork!.SaveChangesAsync();
		await documentChecklist.AssignApplicableChecklistFromAutomationAsync(application.Id,
			new AdmissionCrmPlaybookApplicabilityDto { Campus = lead.CampusBranchName, DesiredClass = lead.DesiredClassName });
		var dto = AdmissionCrmRegistrationModelMapper.ToApplicationDto(application);
		dto.ApplicantStatusPath = statusPath;
		return dto;
	}
}
