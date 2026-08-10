using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmRegistrationQueryWorkflow(
	IRepository<AdmissionCrmRegistrationApplication, Guid> repository,
	AdmissionCrmRegistrationAccess access,
	AdmissionCrmApplicantStatusLinkWorkflow statusLinks) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmRegistrationApplicationDto> GetAsync(Guid id)
	{
		var application = await access.GetApplicationAsync(id, "GetApplicationAsync");
		var dto = AdmissionCrmRegistrationModelMapper.ToApplicationDto(application);
		dto.ApplicantStatusPath = await statusLinks.GetExistingAsync(application.LeadId);
		return dto;
	}

	public async Task<AdmissionCrmRegistrationApplicationDto> GetCurrentByLeadAsync(Guid leadId)
	{
		await access.GetLeadAsync(leadId, "GetCurrentApplicationByLeadAsync");
		var queryable = await repository.GetQueryableAsync();
		var applications = await AsyncExecuter.ToListAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id && x.LeadId == leadId));
		var application = applications.OrderBy(x => x.Status == AdmissionCrmRegistrationStatus.Revoked)
			.ThenByDescending(x => x.IssuedAt).FirstOrDefault()
			?? throw new BusinessException("AdmissionCrm:RegistrationApplicationNotFound");
		var dto = AdmissionCrmRegistrationModelMapper.ToApplicationDto(application);
		dto.ApplicantStatusPath = await statusLinks.GetExistingAsync(application.LeadId);
		return dto;
	}
}
