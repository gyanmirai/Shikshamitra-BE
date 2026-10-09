using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.Students.StudentRecords;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using P = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students;

namespace ShikshaMitra.Students.Dashboard;

[RemoteService(false)]
public class StudentModuleDashboardRecordsReader(IStudentRepository students,
	StudentRecordsEnrollmentScopeService scope, IRepository<StudentOnboardingCase, Guid> cases,
	IRepository<StudentRecordLegacyOverride, Guid> overrides, IRepository<StudentEnrollment, Guid> allEnrollments) : ApplicationService
{
	public async Task FillAsync(StudentModuleDashboardSectionDto result)
	{
		await AuthorizationService.CheckAsync(P.Records_StudentList.View);
		var query = await scope.ApplyStudentScopeAsync(await students.GetQueryableWithoutAutoIncludesAsync());
		var onboardingEligibleIds = await LazyServiceProvider.LazyGetRequiredService<StudentRecordsAccessService>().GetOnboardingEligibleStudentIdsAsync();
		var legacy = await overrides.GetQueryableAsync();
		var enrollments = (await scope.GetQueryableAsync()).Where(e => e.AcademicYearId == result.AcademicYearId);
		var allEnrollmentQuery = await allEnrollments.GetQueryableAsync();
		// Match permanent directory eligibility and historical enrollment membership.
		query = query.Where(s => (s.Status == StudentLifeCycleStatusEnum.Enrolled || s.Status == StudentLifeCycleStatusEnum.InActive || s.Status == StudentLifeCycleStatusEnum.Left) &&
			(onboardingEligibleIds.Contains(s.Id) ||
			 legacy.Any(o => o.TenantId == CurrentTenant.Id && o.StudentId == s.Id && o.Status == "Approved")) &&
			(enrollments.Any(e => e.StudentId == s.Id) || s.Status == StudentLifeCycleStatusEnum.Left && s.AcademicYearId == result.AcademicYearId &&
			 !allEnrollmentQuery.Any(e => e.TenantId == CurrentTenant.Id && e.StudentId == s.Id)));
		var counts = await AsyncExecuter.ToListAsync(query.GroupBy(s => s.Status).Select(group => new { Status = group.Key, Count = group.Count() }));
		int Count(StudentLifeCycleStatusEnum status) => counts.FirstOrDefault(row => row.Status == status)?.Count ?? 0;
		result.Description = "Permanent student records in the selected academic year and your academic scope.";
		result.Metrics =
		[
			Metric("active", "Active students", Count(StudentLifeCycleStatusEnum.Enrolled), "/student/records/directory?population=Active"),
			Metric("inactive", "Inactive students", Count(StudentLifeCycleStatusEnum.InActive), "/student/records/directory?population=Former"),
			Metric("left", "Students who left", Count(StudentLifeCycleStatusEnum.Left), "/student/records/directory?population=Former")
		];
		var strength = await AsyncExecuter.ToListAsync(enrollments.Where(e => e.IsActive && query.Any(s => s.Id == e.StudentId && s.Status == StudentLifeCycleStatusEnum.Enrolled))
			.GroupBy(e => e.StandardId).Select(group => new { StandardId = group.Key, Count = group.Select(e => e.StudentId).Distinct().Count() })
			.OrderByDescending(row => row.Count).ThenBy(row => row.StandardId).Take(12));
		var standardIds = strength.Where(row => row.StandardId.HasValue).Select(row => row.StandardId!.Value).ToArray();
		var standards = await LazyServiceProvider.LazyGetRequiredService<IRepository<ShikshaMitra.TenantSettings.Standards.Standard, Guid>>().GetQueryableAsync();
		var names = await AsyncExecuter.ToListAsync(standards.Where(s => s.TenantId == CurrentTenant.Id && Enumerable.Contains(standardIds, s.Id)).Select(s => new { s.Id, s.Name }));
		result.ClassStrength = strength.Select(row => new StudentModuleDashboardStrengthDto
		{
			StandardId = row.StandardId, Label = names.FirstOrDefault(s => s.Id == row.StandardId)?.Name ?? "Unassigned class", Students = row.Count,
			Href = row.StandardId.HasValue ? $"/student/records/directory?population=Active&class={row.StandardId:D}" : "/student/records/directory?population=Active"
		}).ToList();
	}

	private static StudentModuleDashboardMetricDto Metric(string id, string label, int value, string href) =>
		new() { Id = id, Label = label, Value = value, Href = href, Permission = P.Records_StudentList.View };
}
