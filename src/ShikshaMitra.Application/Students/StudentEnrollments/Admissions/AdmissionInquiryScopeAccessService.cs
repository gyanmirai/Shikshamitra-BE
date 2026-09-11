using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[RemoteService(false)]
public class AdmissionInquiryScopeAccessService(
	IRepository<AdmissionInquiry, Guid> inquiries,
	IRepository<AdmissionCycle, Guid> cycles,
	AdmissionApplicationScopeAccessService applicationScope,
	IUserScopeAccessService userScopes) : ApplicationService, ITransientDependency
{
	public async Task<IQueryable<AdmissionInquiry>> GetScopedQueryAsync()
	{
		var query = await inquiries.GetQueryableAsync();
		var scope = await userScopes.GetCurrentUserScopeAsync();
		if (scope.IsAdmin || scope.HasWildcardAcademicYear) return query;
		if (!scope.HasAnyAccess) return query.Where(_ => false);

		var scopedOfferings = await applicationScope.GetScopedOfferingsAsync();
		var cycleQuery = await cycles.GetQueryableAsync();
		var academicYearIds = scope.AcademicYearIds.Concat(scope.WildcardBoardAcademicYearIds).Distinct().ToList();
		return query.Where(inquiry =>
			(inquiry.AdmissionOfferingId.HasValue && scopedOfferings.Any(offering => offering.Id == inquiry.AdmissionOfferingId.Value)) ||
			(!inquiry.AdmissionOfferingId.HasValue && academicYearIds.Contains(
				cycleQuery.Where(cycle => cycle.Id == inquiry.AdmissionCycleId).Select(cycle => cycle.AcademicYearId).FirstOrDefault())));
	}

	public async Task<AdmissionInquiry> GetAsync(Guid id)
	{
		var inquiry = await AsyncExecuter.FirstOrDefaultAsync((await GetScopedQueryAsync()).Where(item => item.Id == id));
		return inquiry ?? throw new EntityNotFoundException(typeof(AdmissionInquiry), id);
	}

	public async Task EnsureOfferingAccessAsync(Guid offeringId)
	{
		if (!await AsyncExecuter.AnyAsync((await applicationScope.GetScopedOfferingsAsync()).Where(item => item.Id == offeringId)))
			throw new EntityNotFoundException(typeof(AdmissionOffering), offeringId);
	}
}
