using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.ClassTeacher;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.Records;
using ShikshaMitra.TenantSettings.Divisions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.Attendance;

/// <summary>Request-time assignment authority, separate from broad academic scope and action permissions.</summary>
[RemoteService(false)]
public class AttendanceWorkspaceAccessService(IUserScopeAccessService scopes,
	IRepository<ClassTeacher, Guid> assignments, IRepository<Employee, Guid> employees,
	IRepository<Division, Guid> divisions, StudentScopeAccessService scope, StudentRecordsActorService actors)
	: ApplicationService, ITransientDependency
{
	public async Task<bool> IsAdminAsync()
	{
		await actors.EnsureActiveAsync();
		return (await scopes.GetCurrentUserScopeAsync()).IsAdmin;
	}

	public async Task<List<AttendanceAssignedClassDto>> AssignedClassesAsync(Guid yearId)
	{
		await actors.EnsureActiveAsync();
		var scopedDivisions = await scope.ApplyCurrentUserDivisionScopeAsync(
			(await divisions.GetQueryableAsync()).Where(d => d.TenantId == CurrentTenant.Id), yearId);
		var query = from assignment in await assignments.GetQueryableAsync()
			join employee in await employees.GetQueryableAsync() on assignment.TeacherId equals employee.Id
			where assignment.TenantId == CurrentTenant.Id && employee.TenantId == CurrentTenant.Id &&
				employee.UserId == CurrentUser.Id && employee.EmployeeStatusType == EmployeeStatusType.Active && assignment.Status == ClassTeacherAssignmentStatus.Active &&
				(!assignment.AcademicYearId.HasValue || assignment.AcademicYearId == yearId) &&
				scopedDivisions.Any(d => d.Id == assignment.DivisionId && d.StandardId == assignment.StandardId)
			select new AttendanceAssignedClassDto { StandardId = assignment.StandardId, DivisionId = assignment.DivisionId };
		return await AsyncExecuter.ToListAsync(query.Distinct().OrderBy(a => a.StandardId).ThenBy(a => a.DivisionId));
	}

	public async Task<bool> CanReadClassAsync(Guid yearId, Guid standardId, Guid divisionId) =>
		await IsAdminAsync() || (await AssignedClassesAsync(yearId)).Any(a => a.StandardId == standardId && a.DivisionId == divisionId);

	public Task<bool> CanMarkClassAsync(Guid yearId, Guid standardId, Guid divisionId) =>
		CanReadClassAsync(yearId, standardId, divisionId);

	public async Task EnsureCanReadClassAsync(Guid yearId, Guid standardId, Guid divisionId)
	{
		// The dated roster also validates the complete academic hierarchy and user scope.
		if (!await CanReadClassAsync(yearId, standardId, divisionId)) throw new AbpAuthorizationException();
	}

	public Task EnsureCanMarkClassAsync(Guid yearId, Guid standardId, Guid divisionId) =>
		EnsureCanReadClassAsync(yearId, standardId, divisionId);

	public async Task<IQueryable<AttendanceDay>> ApplyReadScopeAsync(IQueryable<AttendanceDay> query)
	{
		if (await IsAdminAsync()) return query;
		var permitted = from assignment in await assignments.GetQueryableAsync()
			join employee in await employees.GetQueryableAsync() on assignment.TeacherId equals employee.Id
			where assignment.TenantId == CurrentTenant.Id && employee.TenantId == CurrentTenant.Id &&
				employee.UserId == CurrentUser.Id && employee.EmployeeStatusType == EmployeeStatusType.Active &&
				assignment.Status == ClassTeacherAssignmentStatus.Active
			select assignment;
		return query.Where(day => permitted.Any(a => a.StandardId == day.StandardId && a.DivisionId == day.DivisionId &&
			(!a.AcademicYearId.HasValue || a.AcademicYearId == day.AcademicYearId)));
	}

	public async Task EnsureAdminAsync()
	{
		if (!await IsAdminAsync()) throw new AbpAuthorizationException();
	}
}
