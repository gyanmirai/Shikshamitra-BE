using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmAnalyticsIdentityReader(
	IRepository<AdmissionCrmApplicantIdentityProfile, Guid> profileRepository,
	IRepository<AdmissionCrmGuardianRelationship, Guid> guardianRepository,
	IRepository<AdmissionCrmPerson, Guid> personRepository) : ApplicationService, ITransientDependency
{
	internal async Task<AdmissionCrmAnalyticsIdentitySnapshot> LoadAsync(Guid yearId, List<Guid> leadIds)
	{
		var profiles = await AsyncExecuter.ToListAsync((await profileRepository.GetQueryableAsync()).Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId && leadIds.Contains(x.LeadId)));
		var profileLeadIds = profiles.Select(x => x.LeadId).ToList();
		var guardians = await AsyncExecuter.ToListAsync((await guardianRepository.GetQueryableAsync()).Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId && x.IsActive && profileLeadIds.Contains(x.LeadId)));
		var personIds = profiles.Select(x => x.ApplicantPersonId).ToList();
		var people = await AsyncExecuter.ToListAsync((await personRepository.GetQueryableAsync()).Where(x =>
			x.TenantId == CurrentTenant.Id && personIds.Contains(x.Id)));
		return new AdmissionCrmAnalyticsIdentitySnapshot(profiles, guardians, people);
	}
}
