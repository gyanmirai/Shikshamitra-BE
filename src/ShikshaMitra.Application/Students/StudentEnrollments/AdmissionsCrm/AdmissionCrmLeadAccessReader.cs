using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmLeadAccessReader(
	IRepository<AdmissionInquiryLead, Guid> leadRepository,
	StudentScopeAccessService studentScopeAccessService) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionInquiryLead> GetAsync(Guid leadId, string operation)
	{
		var lead = await leadRepository.FindAsync(leadId);
		if (lead == null || lead.TenantId != CurrentTenant.Id)
		{
			throw new EntityNotFoundException(typeof(AdmissionInquiryLead), leadId);
		}

		await studentScopeAccessService.EnsureCanAccessAdmissionLeadAsync(lead, operation);
		return lead;
	}
}
