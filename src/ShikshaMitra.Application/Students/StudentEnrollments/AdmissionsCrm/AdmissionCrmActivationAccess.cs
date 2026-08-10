using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmActivationAccess(
	IRepository<AdmissionInquiryLead, Guid> leadRepository,
	IRepository<AdmissionCrmActivation, Guid> activationRepository,
	IRepository<AdmissionCrmNumberSeries, Guid> seriesRepository,
	StudentScopeAccessService scopeAccess) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionInquiryLead> GetLeadAsync(Guid id, string operation)
	{
		var lead = await leadRepository.GetAsync(id);
		await scopeAccess.EnsureCanAccessAdmissionLeadAsync(lead, operation);
		return lead;
	}

	public async Task<AdmissionCrmActivation> GetActivationAsync(Guid id, string operation)
	{
		var activation = await activationRepository.GetAsync(id);
		await GetLeadAsync(activation.LeadId, operation);
		return activation;
	}

	public async Task<AdmissionCrmActivation?> FindActivationAsync(Guid leadId)
	{
		var query = await activationRepository.GetQueryableAsync();
		return await AsyncExecuter.FirstOrDefaultAsync(query.Where(x =>
			x.TenantId == CurrentTenant.Id && x.LeadId == leadId));
	}

	public async Task<AdmissionCrmNumberSeries> GetSeriesAsync(Guid id)
	{
		var series = await seriesRepository.GetAsync(id);
		if (series.TenantId != CurrentTenant.Id)
			throw new AbpAuthorizationException("Number series belongs to another tenant.");
		await scopeAccess.EnsureCanAccessAcademicYearAsync(series.AcademicYearId, "GetSeriesAsync");
		return series;
	}
}
