using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmAnalyticsGovernanceReader(
	IRepository<AdmissionCrmPolicyPack, Guid> packRepository,
	IRepository<AdmissionCrmPolicyAdoption, Guid> adoptionRepository,
	IRepository<AdmissionCrmPolicyEvaluation, Guid> evaluationRepository,
	IRepository<AdmissionCrmPolicyException, Guid> exceptionRepository,
	IRepository<AdmissionCrmRouteAssignment, Guid> routeRepository,
	IRepository<AdmissionCrmRouteAppeal, Guid> appealRepository,
	IRepository<AdmissionCrmActivation, Guid> activationRepository,
	IRepository<AdmissionCrmIdentifierAllocation, Guid> identifierRepository) : ApplicationService, ITransientDependency
{
	internal async Task<AdmissionCrmAnalyticsGovernanceSnapshot> LoadAsync(Guid yearId, List<Guid> leadIds)
	{
		var packs = await AsyncExecuter.ToListAsync((await packRepository.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id));
		var adoptions = await AsyncExecuter.ToListAsync((await adoptionRepository.GetQueryableAsync()).Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId));
		var evaluations = await AsyncExecuter.ToListAsync((await evaluationRepository.GetQueryableAsync()).Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId && leadIds.Contains(x.LeadId)));
		var exceptions = await AsyncExecuter.ToListAsync((await exceptionRepository.GetQueryableAsync()).Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId && leadIds.Contains(x.LeadId)));
		var routes = await AsyncExecuter.ToListAsync((await routeRepository.GetQueryableAsync()).Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId && leadIds.Contains(x.LeadId)));
		var appeals = await AsyncExecuter.ToListAsync((await appealRepository.GetQueryableAsync()).Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId && leadIds.Contains(x.LeadId)));
		var activations = await AsyncExecuter.ToListAsync((await activationRepository.GetQueryableAsync()).Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId && leadIds.Contains(x.LeadId)));
		var identifiers = await AsyncExecuter.ToListAsync((await identifierRepository.GetQueryableAsync()).Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId && leadIds.Contains(x.LeadId)));
		return new AdmissionCrmAnalyticsGovernanceSnapshot(packs, adoptions, evaluations, exceptions,
			routes, appeals, activations, identifiers);
	}
}
