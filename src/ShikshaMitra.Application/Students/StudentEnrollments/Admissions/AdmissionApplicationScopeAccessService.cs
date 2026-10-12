using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.Divisions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>Authorizes application operations against Offering and linked student placement scope.</summary>
[RemoteService(false)]
public class AdmissionApplicationScopeAccessService(
	IRepository<AdmissionOffering, Guid> offerings,
	IRepository<AdmissionCycle, Guid> cycles,
	IRepository<Division, Guid> divisions,
	IUserScopeAccessService userScopes,
	IRepository<AdmissionApplication, Guid> applications,
	AdmissionApplicationPlacementScopeService placementScope) : ApplicationService, ITransientDependency
{
	public async Task<IQueryable<AdmissionOffering>> GetScopedOfferingsAsync() =>
		AdmissionOfferingScopeQuery.Apply((await offerings.GetQueryableAsync()).Where(offering => offering.TenantId == CurrentTenant.Id),
			(await cycles.GetQueryableAsync()).Where(cycle => cycle.TenantId == CurrentTenant.Id),
			(await divisions.GetQueryableAsync()).Where(division => division.TenantId == CurrentTenant.Id), await userScopes.GetCurrentUserScopeAsync());

	public async Task<IQueryable<AdmissionApplication>> GetScopedApplicationsAsync()
	{
		var offerings = await GetScopedOfferingsAsync();
		return await placementScope.ApplyAsync((await applications.GetQueryableAsync()).Where(application =>
			offerings.Any(offering => offering.Id == application.AdmissionOfferingId)));
	}

	public async Task<bool> CanAccessAsync(AdmissionApplication application)
	{
		if (application.TenantId != CurrentTenant.Id)
			return false;
		var query = await GetScopedApplicationsAsync();
		return await AsyncExecuter.AnyAsync(query.Where(item => item.Id == application.Id));
	}

	public async Task EnsureCanAccessAsync(AdmissionApplication application)
	{
		if (!await CanAccessAsync(application))
			throw new EntityNotFoundException(typeof(AdmissionApplication), application.Id);
	}
}
