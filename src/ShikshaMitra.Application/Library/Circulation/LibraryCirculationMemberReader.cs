using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Library.Members;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Library.Circulation;

[RemoteService(false)]
public class LibraryCirculationMemberReader(ILibraryAccessService access, IUserScopeAccessService scopes,
	IRepository<LibraryMember, Guid> memberships, IRepository<Standard, Guid> standards,
	IRepository<Division, Guid> divisions, LibraryMemberStatisticsReader statistics) : ApplicationService, ITransientDependency
{
	public async Task<LibraryCirculationMemberDto> ResolveAsync(string identifier)
	{
		var term = (identifier ?? string.Empty).Trim();
		if (term.Length is 0 or > 100) throw new BusinessException(LibraryErrorCodes.MemberNotFound);
		var scope = await ReadScopeAsync();
		var members = await memberships.GetQueryableAsync();
		var normalized = term.ToLowerInvariant();
		var isGuid = Guid.TryParse(term, out var id);
		var matchingStudents = members.Where(x => x.MemberIdentifier.ToLower() == normalized || isGuid && x.Id == id)
			.Select(x => x.StudentId);
		var studentRows = await AsyncExecuter.ToListAsync((await access.GetStudentsAsync(scope))
			.Where(x => isGuid && x.Id == id || matchingStudents.Contains(x.Id)).OrderBy(x => x.Id)
			.Select(LibraryCirculationBorrowerProjection.Student).Take(2));
		var employeeRows = await AsyncExecuter.ToListAsync((await access.GetEmployeesAsync(scope))
			.Where(x => isGuid && x.Id == id || x.EmployeeCode != null && x.EmployeeCode.ToLower() == normalized).OrderBy(x => x.Id)
			.Select(LibraryCirculationBorrowerProjection.Employee).Take(2));
		if (studentRows.Count + employeeRows.Count == 0) throw new BusinessException(LibraryErrorCodes.MemberNotFound);
		if (studentRows.Count + employeeRows.Count != 1)
			throw new BusinessException(LibraryErrorCodes.MemberIdentifierAmbiguous,
				"More than one borrower uses this identifier. Search and select the correct borrower.");
		return (await MapAsync(studentRows, employeeRows)).Single();
	}

	public async Task<PaginatedResponse<LibraryCirculationMemberDto>> SearchAsync(string? search, int page, int pageSize)
	{
		var scope = await ReadScopeAsync();
		page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
		var studentQuery = await access.GetStudentsAsync(scope);
		var employeeQuery = await access.GetEmployeesAsync(scope);
		var term = (search ?? string.Empty).Trim().ToLowerInvariant();
		if (term.Length > 100) throw new BusinessException(LibraryErrorCodes.MemberNotFound);
		if (term.Length > 0)
		{
			var isGuid = Guid.TryParse(term, out var id);
			var matchingStudents = (await memberships.GetQueryableAsync()).Where(x =>
				x.MemberIdentifier.ToLower().Contains(term) || isGuid && x.Id == id).Select(x => x.StudentId);
			studentQuery = studentQuery.Where(x => isGuid && x.Id == id || matchingStudents.Contains(x.Id) ||
				x.FirstName != null && x.FirstName.ToLower().Contains(term) ||
				x.MiddleName != null && x.MiddleName.ToLower().Contains(term) || x.LastName != null && x.LastName.ToLower().Contains(term));
			employeeQuery = employeeQuery.Where(x => isGuid && x.Id == id ||
				x.EmployeeCode != null && x.EmployeeCode.ToLower().Contains(term) ||
				x.FirstName != null && x.FirstName.ToLower().Contains(term) ||
				x.MiddleName != null && x.MiddleName.ToLower().Contains(term) || x.LastName != null && x.LastName.ToLower().Contains(term));
		}
		var studentCount = await AsyncExecuter.CountAsync(studentQuery);
		var count = studentCount + await AsyncExecuter.CountAsync(employeeQuery);
		var skip = (page - 1) * pageSize;
		var students = skip < studentCount
			? await AsyncExecuter.ToListAsync(studentQuery.OrderBy(x => x.FirstName).ThenBy(x => x.LastName).ThenBy(x => x.Id)
				.Skip(skip).Take(Math.Min(pageSize, studentCount - skip)).Select(LibraryCirculationBorrowerProjection.Student))
			: new List<CirculationStudentRow>();
		var employees = students.Count < pageSize
			? await AsyncExecuter.ToListAsync(employeeQuery.OrderBy(x => x.FirstName).ThenBy(x => x.LastName).ThenBy(x => x.Id)
				.Skip(Math.Max(0, skip - studentCount)).Take(pageSize - students.Count).Select(LibraryCirculationBorrowerProjection.Employee))
			: new List<CirculationEmployeeRow>();
		return new()
		{
			TotalCount = count, PageSize = pageSize, CurrentPage = page,
			TotalPages = (int)Math.Ceiling(count / (double)pageSize), Items = await MapAsync(students, employees)
		};
	}

	private async Task<UserScopeContext> ReadScopeAsync()
	{
		var scope = await scopes.GetCurrentUserScopeAsync();
		scopes.EnsureHasAnyAccess(scope, UserScopeErrorCodes.AccessDenied);
		if (scope.TenantId != CurrentTenant.Id) throw new BusinessException(UserScopeErrorCodes.AccessDenied);
		return scope;
	}

	private async Task<List<LibraryCirculationMemberDto>> MapAsync(List<CirculationStudentRow> students, List<CirculationEmployeeRow> employees)
	{
		var ids = students.Select(x => x.Id).Concat(employees.Select(x => x.Id)).ToList();
		var stats = await statistics.ReadAsync(ids);
		var studentIds = students.Select(x => x.Id).ToList();
		var latest = (await AsyncExecuter.ToListAsync((await memberships.GetQueryableAsync())
			.Where(x => studentIds.Contains(x.StudentId)).Select(x => new { x.Id, x.StudentId, x.MemberIdentifier, x.IsActive, x.CreationTime })))
			.GroupBy(x => x.StudentId)
			.ToDictionary(x => x.Key, x => x.OrderByDescending(row => row.CreationTime).ThenBy(row => row.Id).First());
		var standardIds = students.Where(x => x.StandardId.HasValue).Select(x => x.StandardId!.Value).Distinct().ToList();
		var divisionIds = students.Where(x => x.DivisionId.HasValue).Select(x => x.DivisionId!.Value).Distinct().ToList();
		var standardNames = (await AsyncExecuter.ToListAsync((await standards.GetQueryableAsync())
			.Where(x => standardIds.Contains(x.Id)).Select(x => new { x.Id, x.Name }))).ToDictionary(x => x.Id, x => x.Name);
		var divisionNames = (await AsyncExecuter.ToListAsync((await divisions.GetQueryableAsync())
			.Where(x => divisionIds.Contains(x.Id)).Select(x => new { x.Id, x.Name }))).ToDictionary(x => x.Id, x => x.Name);
		var result = students.Select(student =>
		{
			latest.TryGetValue(student.Id, out var membership);
			var reason = student.Status is StudentLifeCycleStatusEnum.Left or StudentLifeCycleStatusEnum.InActive
				? "This student is not active. Existing loans can still be reviewed by an authorized librarian."
				: !student.IsLibraryMember || membership?.IsActive == false ? "Library membership is not active." : null;
			var standard = student.StandardId.HasValue ? standardNames.GetValueOrDefault(student.StandardId.Value) : null;
			var division = student.DivisionId.HasValue ? divisionNames.GetValueOrDefault(student.DivisionId.Value) : null;
			return new LibraryCirculationMemberDto
			{
				Id = student.Id, MemberIdentifier = membership?.MemberIdentifier, Role = 0, AcademicYearId = student.AcademicYearId,
				MemberName = Name(student.FirstName, student.MiddleName, student.LastName),
				ClassDivision = standard != null && division != null ? $"{standard}-{division}" : standard ?? division,
				IsEligibleForIssue = reason == null, IneligibilityReason = reason, Statistics = stats[student.Id]
			};
		}).ToList();
		result.AddRange(employees.Select(employee =>
		{
			var reason = employee.EmployeeStatusType != EmployeeStatusType.Active
				? "This staff member is not active. Existing loans can still be reviewed by an authorized librarian."
				: !employee.IsLibraryMember ? "Library membership is not active." : null;
			return new LibraryCirculationMemberDto
			{
				Id = employee.Id, MemberIdentifier = employee.EmployeeCode, Role = 1,
				MemberName = Name(employee.FirstName, employee.MiddleName, employee.LastName),
				IsEligibleForIssue = reason == null, IneligibilityReason = reason, Statistics = stats[employee.Id]
			};
		}));
		return result;
	}

	private static string Name(params string?[] parts) =>
		string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
}
