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

/// <summary>Authorizes application operations against the complete offering scope.</summary>
[RemoteService(false)]
public class AdmissionApplicationScopeAccessService(
	IRepository<AdmissionOffering, Guid> offerings,
	IRepository<AdmissionCycle, Guid> cycles,
	IRepository<Division, Guid> divisions,
	IUserScopeAccessService userScopes) : ApplicationService, ITransientDependency
{
	public async Task<IQueryable<AdmissionOffering>> GetScopedOfferingsAsync() =>
		AdmissionOfferingScopeQuery.Apply(await offerings.GetQueryableAsync(), await cycles.GetQueryableAsync(),
			await divisions.GetQueryableAsync(), await userScopes.GetCurrentUserScopeAsync());

	public async Task EnsureCanAccessAsync(AdmissionApplication application)
	{
		var query = await GetScopedOfferingsAsync();
		if (application.TenantId != CurrentTenant.Id ||
			!await AsyncExecuter.AnyAsync(query.Where(item => item.Id == application.AdmissionOfferingId)))
			throw new EntityNotFoundException(typeof(AdmissionApplication), application.Id);
	}
}
