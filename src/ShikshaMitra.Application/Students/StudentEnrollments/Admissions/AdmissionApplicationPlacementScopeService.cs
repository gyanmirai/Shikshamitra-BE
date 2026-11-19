using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[RemoteService(false)]
public class AdmissionApplicationPlacementScopeService(
	IUserScopeAccessService scopes, StudentRecordsEnrollmentScopeService recordScope,
	IRepository<StudentEnrollment, Guid> enrollments, IRepository<StudentOnboardingCase, Guid> cases,
	IRepository<AdmissionCycle, Guid> cycles, IStudentRepository students, StudentScopeAccessService studentScope)
	: ApplicationService, ITransientDependency
{
	public async Task<IQueryable<AdmissionApplication>> ApplyAsync(IQueryable<AdmissionApplication> query)
	{
		query = query.Where(application => application.TenantId == CurrentTenant.Id);
		var scope = await scopes.GetCurrentUserScopeAsync();
		if (scope.IsAdmin || scope.HasTenantWideScope()) return query;
		if (!scope.HasAnyAccess) return query.Where(_ => false);
		var allEnrollments = (await enrollments.GetQueryableAsync()).Where(item => item.TenantId == CurrentTenant.Id);
		var visibleEnrollments = await recordScope.GetQueryableAsync();
		var allCases = (await cases.GetQueryableAsync()).Where(item => item.TenantId == CurrentTenant.Id);
		var visibleCases = allCases.Where(item => !item.StudentEnrollmentId.HasValue &&
			(!item.DivisionId.HasValue || scope.AcademicYearIds.Contains(item.AcademicYearId) &&
				(scope.WildcardBoardAcademicYearIds.Contains(item.AcademicYearId) || scope.WildcardMediumBoardIds.Contains(item.IntendedBoardId) ||
				 scope.WildcardMediumStageMediumIds.Contains(item.IntendedMediumId) || scope.WildcardStandardMediumStageIds.Contains(item.IntendedStageId) ||
				 scope.WildcardDivisionStandardIds.Contains(item.IntendedStandardId) || scope.DivisionIds.Contains(item.DivisionId.Value))));
		var visibleStudents = await studentScope.ApplyCurrentUserScopeAsync(await students.GetQueryableWithoutAutoIncludesAsync());
		var years = (await cycles.GetQueryableAsync()).Where(item => item.TenantId == CurrentTenant.Id);
		// Enrollment placement wins over the completed case snapshot after a Records division change.
		return query.Where(application => !(application.StudentId ?? application.EnrolledStudentId).HasValue || years.Any(year =>
			year.Id == application.AdmissionCycleId &&
			(allEnrollments.Any(item => item.StudentId == (application.StudentId ?? application.EnrolledStudentId) && item.AcademicYearId == year.AcademicYearId)
				? visibleEnrollments.Any(item => item.StudentId == (application.StudentId ?? application.EnrolledStudentId) && item.AcademicYearId == year.AcademicYearId)
				: allCases.Any(item => item.Id == application.OnboardingCaseId && item.ApplicationId == application.Id &&
					item.StudentId == (application.StudentId ?? application.EnrolledStudentId) && item.AcademicYearId == year.AcademicYearId)
					? visibleCases.Any(item => item.Id == application.OnboardingCaseId && item.ApplicationId == application.Id &&
						item.StudentId == (application.StudentId ?? application.EnrolledStudentId) && item.AcademicYearId == year.AcademicYearId)
					: visibleStudents.Any(item => item.Id == (application.StudentId ?? application.EnrolledStudentId) && item.AcademicYearId == year.AcademicYearId))));
	}
}
