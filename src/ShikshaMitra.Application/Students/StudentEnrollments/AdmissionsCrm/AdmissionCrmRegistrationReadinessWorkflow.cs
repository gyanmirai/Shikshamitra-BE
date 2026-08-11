using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmRegistrationReadinessWorkflow(
	AdmissionCrmRegistrationAccess access,
	AdmissionCrmRegistrationEligibilityWorkflow eligibility,
	IRepository<AdmissionCrmRegistrationTemplate, Guid> templateRepository,
	IRepository<AdmissionCrmFormNumberSeries, Guid> seriesRepository) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmRegistrationReadinessDto> GetAsync(Guid leadId)
	{
		var lead = await access.GetLeadAsync(leadId, "GetRegistrationReadinessAsync");
		var path = await eligibility.ResolvePathAsync(lead, null);
		var blockers = new List<AdmissionCrmRegistrationReadinessBlockerDto>(
			await eligibility.EvaluateAsync(lead, path, false));

		AddRequiredLeadBlockers(lead, blockers);
		var actual = new AdmissionCrmPlaybookApplicabilityDto
		{
			Campus = lead.CampusBranchName,
			DesiredClass = lead.DesiredClassName,
			AdmissionRoute = lead.IntendedRouteNameSnapshot ?? lead.IntendedRouteType?.ToString()
		};

		var templateQuery = await templateRepository.GetQueryableAsync();
		var templates = await AsyncExecuter.ToListAsync(templateQuery.Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == lead.AcademicYearId &&
			x.Status == AdmissionCrmRegistrationTemplateStatus.Published));
		var hasTemplate = templates.Any(item => AdmissionCrmPlaybookApplicability.Matches(
			AdmissionCrmRegistrationModelMapper.Deserialize<AdmissionCrmPlaybookApplicabilityDto>(item.ApplicabilityJson), actual));
		if (!hasTemplate)
			blockers.Add(Blocker("AdmissionCrm:PublishedRegistrationTemplateRequired", "Publish an applicable registration template before issuing registration.", "configure-registration-template"));

		var seriesQuery = await seriesRepository.GetQueryableAsync();
		var series = await AsyncExecuter.ToListAsync(seriesQuery.Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == lead.AcademicYearId && x.IsActive));
		var hasSeries = series.Any(item => AdmissionCrmPlaybookApplicability.Matches(
			AdmissionCrmRegistrationModelMapper.Deserialize<AdmissionCrmPlaybookApplicabilityDto>(item.ApplicabilityJson), actual));
		if (!hasSeries)
			blockers.Add(Blocker("AdmissionCrm:FormNumberSeriesOrManualNumberRequired", "Configure an applicable form-number series or enter a manual form number.", "configure-form-number-series"));

		return new AdmissionCrmRegistrationReadinessDto
		{
			CanIssue = blockers.Count == 0,
			RegistrationPath = path,
			Blockers = blockers
		};
	}

	private static void AddRequiredLeadBlockers(
		AdmissionInquiryLead lead,
		ICollection<AdmissionCrmRegistrationReadinessBlockerDto> blockers)
	{
		if (string.IsNullOrWhiteSpace(lead.FirstName))
			blockers.Add(Blocker("AdmissionCrm:StudentNameRequired", "Capture the applicant's name before issuing registration.", "edit-inquiry"));
		if (string.IsNullOrWhiteSpace(lead.ParentGuardianName))
			blockers.Add(Blocker("AdmissionCrm:ParentGuardianNameRequired", "Capture a parent or guardian before issuing registration.", "edit-inquiry"));
		if (!lead.InquirySourceId.HasValue || string.IsNullOrWhiteSpace(lead.InquirySourceSnapshot))
			blockers.Add(Blocker("AdmissionCrm:InquirySourceRequired", "Select an inquiry source before issuing registration.", "edit-inquiry"));
		if (string.IsNullOrWhiteSpace(lead.PrimaryContactNo) && string.IsNullOrWhiteSpace(lead.Email))
			blockers.Add(Blocker("AdmissionCrm:ApplicantContactRequired", "Capture a phone number or email before issuing registration.", "edit-inquiry"));
		if (!lead.TargetStandardId.HasValue)
			blockers.Add(Blocker("AdmissionCrm:TargetStandardRequired", "Select the target class before issuing registration.", "edit-inquiry"));
	}

	private static AdmissionCrmRegistrationReadinessBlockerDto Blocker(
		string code,
		string message,
		string actionCode) => new() { Code = code, Message = message, ActionCode = actionCode };
}
