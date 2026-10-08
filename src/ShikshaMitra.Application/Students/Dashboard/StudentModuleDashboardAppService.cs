using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using P = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students;

namespace ShikshaMitra.Students.Dashboard;

[RemoteService(false)]
[Authorize(P.Dashboard.View)]
public class StudentModuleDashboardAppService(StudentRecordsActorService actors, StudentRecordsAccessService access,
	IUserScopeAccessService scopes, IRepository<AcademicYear, Guid> years, StudentPortalAccessService portal,
	StudentModuleDashboardRecordsReader records, StudentModuleDashboardAdmissionsReader admissions,
	StudentModuleDashboardAttendanceReader attendance) : ApplicationService, IStudentModuleDashboardAppService
{
	public async Task<StudentModuleDashboardContextDto> GetContextAsync()
	{
		var user = await actors.EnsureActiveAsync();
		var roles = await access.GetDatabaseRoleNamesAsync();
		var context = new StudentModuleDashboardContextDto
		{ Mode = StudentModuleDashboardCatalog.ResolveMode(roles), DisplayName = user.Name ?? user.UserName };
		// Portal identities use persisted, verified associations, never broad staff scope.
		if (roles.Count > 0 && roles.All(role => role.Equals("student", StringComparison.OrdinalIgnoreCase) || role.Equals("parent", StringComparison.OrdinalIgnoreCase)))
		{
			context.Mode = "common";
			context.ScopeLabel = "Your linked student accounts";
			context.MyStudents = (await portal.GetMyStudentsAsync()).GroupBy(s => s.StudentId)
				.Select(group => new StudentModuleDashboardPersonalStudentDto
				{ StudentId = group.Key, Name = group.First().StudentName, Relationship = group.First().Relationship }).ToList();
			return context;
		}
		var scope = await scopes.GetCurrentUserScopeAsync();
		context.ScopeLabel = !scope.HasAnyAccess ? "No academic scope is assigned" : scope.HasTenantWideScope() ? "School-wide access · permissions apply" : "Your assigned academic scope";
		foreach (var link in StudentModuleDashboardCatalog.Links)
			if (await AuthorizationService.IsGrantedAsync(link.Permission) && await AuthorizationService.IsGrantedAsync(link.VisibilityPermission))
				context.Links.Add(link);
		if (!scope.HasAnyAccess) return context;
		if (context.Links.Any(link => link.Id == "directory")) context.Sections.Add("records");
		if (context.Links.Any(link => link.Id is "inquiries" or "applications" or "onboarding")) context.Sections.Add("admissions");
		if (context.Links.Any(link => link.Id is "attendance" or "leave")) context.Sections.Add("attendance");
		return context;
	}

	public async Task<StudentModuleDashboardSectionDto> GetSectionAsync(string section, Guid academicYearId)
	{
		var context = await GetContextAsync();
		if (section is not ("records" or "admissions" or "attendance"))
			throw new BusinessException(StudentModuleDashboardErrorCodes.InvalidSection);
		if (!context.Sections.Contains(section)) throw new AbpAuthorizationException();
		var year = await years.FindAsync(year => year.Id == academicYearId && year.TenantId == CurrentTenant.Id);
		if (year == null) throw new AbpAuthorizationException();
		var scope = await scopes.GetCurrentUserScopeAsync();
		if (!scope.HasAnyAccess || (!scope.HasTenantWideScope() && !scope.AcademicYearIds.Contains(academicYearId)))
			throw new AbpAuthorizationException();
		var result = new StudentModuleDashboardSectionDto { Key = section, AcademicYearId = academicYearId, GeneratedAt = Clock.Now };
		// One DbContext is used: aggregate queries run sequentially and project only bounded results.
		switch (section)
		{
			case "records": await records.FillAsync(result); break;
			case "admissions": await admissions.FillAsync(result, context); break;
			case "attendance": await attendance.FillAsync(result, context); break;
		}
		return result;
	}
}
