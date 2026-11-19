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
	IUserScopeAccessService userScopes,
	IRepository<AdmissionApplication, Guid> applications) : ApplicationService, ITransientDependency
{
	public async Task<IQueryable<AdmissionInquiry>> GetScopedQueryAsync()
	{
		var query = (await inquiries.GetQueryableAsync()).Where(inquiry => inquiry.TenantId == CurrentTenant.Id);
		var scope = await userScopes.GetCurrentUserScopeAsync();
		if (scope.IsAdmin || scope.HasWildcardAcademicYear) return query;
		if (!scope.HasAnyAccess) return query.Where(_ => false);

		var scopedOfferings = await applicationScope.GetScopedOfferingsAsync();
		var scopedApplications = await applicationScope.GetScopedApplicationsAsync();
		var linkedApplications = (await applications.GetQueryableAsync()).Where(item => item.TenantId == CurrentTenant.Id);
		query = query.Where(inquiry => !linkedApplications.Any(application =>
			(application.StudentId.HasValue || application.EnrolledStudentId.HasValue) &&
			(application.InquiryId == inquiry.Id || application.ApplicantId == inquiry.ApplicantId) &&
			!scopedApplications.Any(allowed => allowed.Id == application.Id)));
		var cycleQuery = await cycles.GetQueryableAsync();
		var academicYearIds = scope.AcademicYearIds.Concat(scope.WildcardBoardAcademicYearIds).Distinct().ToList();
		return query.Where(inquiry =>
			(inquiry.AdmissionOfferingId.HasValue && scopedOfferings.Any(offering => offering.Id == inquiry.AdmissionOfferingId.Value)) ||
			(!inquiry.AdmissionOfferingId.HasValue && cycleQuery.Any(cycle => cycle.Id == inquiry.AdmissionCycleId &&
				academicYearIds.Contains(cycle.AcademicYearId) &&
				(scope.WildcardBoardAcademicYearIds.Contains(cycle.AcademicYearId) ||
				 inquiry.AssignedCounsellorId == scope.UserId || inquiry.CreatorId == scope.UserId))));
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
