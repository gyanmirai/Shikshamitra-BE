using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmRegistrationAutomationWorkflow(
	IRepository<AdmissionCrmRegistrationApplication, Guid> applicationRepository,
	IRepository<AdmissionCrmRegistrationTemplate, Guid> templateRepository,
	AdmissionCrmRegistrationAccess access,
	AdmissionCrmRegistrationIssuanceWorkflow issuance) : ApplicationService, ITransientDependency
{
	public async Task<bool> IssueApplicableAsync(Guid leadId, AdmissionInquiryStage stage)
	{
		var lead = await access.GetLeadAsync(leadId, "IssueApplicableFromAutomationAsync");
		var applications = await applicationRepository.GetQueryableAsync();
		if (await AsyncExecuter.AnyAsync(applications.Where(x => x.TenantId == CurrentTenant.Id && x.LeadId == leadId &&
			x.Status != AdmissionCrmRegistrationStatus.Revoked))) return false;
		var queryable = await templateRepository.GetQueryableAsync();
		var candidates = await AsyncExecuter.ToListAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == lead.AcademicYearId && x.Status == AdmissionCrmRegistrationTemplateStatus.Published && x.AutoIssueAtStage == stage));
		var actual = new AdmissionCrmPlaybookApplicabilityDto { Campus = lead.CampusBranchName, DesiredClass = lead.DesiredClassName };
		var template = candidates.Select(item => new { Item = item,
			Scope = AdmissionCrmRegistrationModelMapper.Deserialize<AdmissionCrmPlaybookApplicabilityDto>(item.ApplicabilityJson) })
			.Where(item => AdmissionCrmPlaybookApplicability.Matches(item.Scope, actual))
			.OrderByDescending(item => AdmissionCrmPlaybookApplicability.Specificity(item.Scope))
			.ThenByDescending(item => item.Item.IsFallback).Select(item => item.Item).FirstOrDefault();
		if (template == null) return false;
		await issuance.IssueAsync(leadId, new IssueAdmissionCrmRegistrationApplicationDto { RegistrationTemplateId = template.Id });
		return true;
	}
}
