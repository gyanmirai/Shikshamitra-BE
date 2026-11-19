using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.ClassTeacher;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Identity;
using Volo.Abp.Data;

namespace ShikshaMitra.Finance;

[RemoteService(false)]
public partial class FinanceScopeAccessService(
	IUserScopeService userScopeService,
	IAcademicYearRepository academicYearRepository,
	IReadOnlyRepository<Student, Guid> studentRepository,
	IRepository<ClassTeacher, Guid> classTeacherRepository,
	IRepository<Employee, Guid> employeeRepository,
	IPermissionChecker permissionChecker,
	IRepository<IdentityUser, Guid> identityUsers,
	IRepository<IdentityRole, Guid> roleRepository,
	IDataFilter dataFilter)
	: ApplicationService, ITransientDependency
{
	public async Task<IQueryable<Guid>> GetRequestScopedStudentIdsQueryAsync(
		Guid academicYearId, string actionPermission, CancellationToken cancellationToken = default)
	{
		if (!CurrentUser.Id.HasValue || !CurrentTenant.Id.HasValue || academicYearId == Guid.Empty ||
			string.IsNullOrWhiteSpace(actionPermission) ||
			!await permissionChecker.IsGrantedAsync(actionPermission))
			throw new AbpAuthorizationException("Finance request access denied.");

		return (await GetRequestScopedStudentsQueryForUserAsync(CurrentUser.Id.Value, academicYearId, cancellationToken))
			.Select(student => student.Id);
	}

	public async Task EnsureRequestStudentAccessibleAsync(
		Guid studentId, Guid academicYearId, string actionPermission, CancellationToken cancellationToken = default)
	{
		if (!await IsRequestStudentAccessibleAsync(studentId, academicYearId, actionPermission, cancellationToken))
			throw new AbpAuthorizationException("Finance request student access denied.");
	}

	public async Task<bool> IsRequestStudentAccessibleAsync(
		Guid studentId, Guid academicYearId, string actionPermission, CancellationToken cancellationToken = default)
	{
		var ids = await GetRequestScopedStudentIdsQueryAsync(academicYearId, actionPermission, cancellationToken);
		return await AsyncExecuter.AnyAsync(ids.Where(id => id == studentId), cancellationToken);
	}

	public async Task<IQueryable<Guid>> GetReviewerScopedStudentIdsQueryAsync(
		Guid academicYearId, string viewPermission, string decisionPermission,
		CancellationToken cancellationToken = default)
	{
		if (!CurrentUser.Id.HasValue || !CurrentTenant.Id.HasValue || academicYearId == Guid.Empty ||
			string.IsNullOrWhiteSpace(viewPermission) || string.IsNullOrWhiteSpace(decisionPermission) ||
			!await permissionChecker.IsGrantedAsync(viewPermission) ||
			!await permissionChecker.IsGrantedAsync(decisionPermission))
			throw new AbpAuthorizationException("Finance reviewer access denied.");
		return (await GetRequestScopedStudentsQueryForUserAsync(
			CurrentUser.Id.Value, academicYearId, cancellationToken, capTeacherAssignments: false))
			.Select(student => student.Id);
	}

	public async Task EnsureReviewerStudentAccessibleAsync(
		Guid studentId, Guid academicYearId, string viewPermission, string decisionPermission,
		CancellationToken cancellationToken = default)
	{
		var scoped = await GetReviewerScopedStudentIdsQueryAsync(
			academicYearId, viewPermission, decisionPermission, cancellationToken);
		if (!await AsyncExecuter.AnyAsync(scoped.Where(id => id == studentId), cancellationToken))
			throw new AbpAuthorizationException("Finance reviewer student access denied.");
	}

	public async Task<bool> IsClassTeacherRequestUserAsync(CancellationToken cancellationToken = default)
	{
		if (!CurrentUser.Id.HasValue || !CurrentTenant.Id.HasValue) return false;
		var employee = await GetCurrentTenantEmployeeAsync(CurrentUser.Id.Value, cancellationToken);
		return employee != null && await classTeacherRepository.AnyAsync(assignment =>
			assignment.TenantId == CurrentTenant.Id && assignment.TeacherId == employee.Id &&
			assignment.Status == ClassTeacherAssignmentStatus.Active, cancellationToken);
	}

	internal async Task<IQueryable<Student>> GetRequestScopedStudentsQueryForUserAsync(
		Guid userId, Guid academicYearId, CancellationToken cancellationToken = default,
		bool capTeacherAssignments = true)
	{
		if (!CurrentTenant.Id.HasValue || academicYearId == Guid.Empty)
			throw new AbpAuthorizationException("Finance request requires a tenant and academic year.");
		var identity = await GetActiveIdentityAsync(userId);
		var scope = await userScopeService.GetUserScopeAsync(userId, cancellationToken);
		var students = (await studentRepository.GetQueryableAsync()).Where(student =>
			student.TenantId == CurrentTenant.Id && student.AcademicYearId == academicYearId);
		students = FinanceStudentScopeQueryBuilder.ApplyPayerScope(students, scope);
		return scope.IsAdmin || !capTeacherAssignments
			? students : await ApplyClassTeacherCapAsync(students, userId, identity, cancellationToken);
	}

	private async Task<IdentityUser> GetActiveIdentityAsync(Guid userId)
	{
		var identity = await identityUsers.FindAsync(userId);
		if (identity == null || !identity.IsActive || identity.TenantId != CurrentTenant.Id)
			throw new AbpAuthorizationException("Finance request identity is unavailable.");
		return identity;
	}

	private async Task<IQueryable<Student>> ApplyClassTeacherCapAsync(
		IQueryable<Student> students, Guid userId, IdentityUser identity,
		CancellationToken cancellationToken)
	{
		var employee = await GetCurrentTenantEmployeeAsync(userId, cancellationToken);
		var hasClassTeacherHistory = await HasClassTeacherHistoryAsync(identity.Id, employee, cancellationToken);
		if (employee == null) return hasClassTeacherHistory ? students.Where(_ => false) : students;
		if (employee.EmployeeStatusType != EmployeeStatusType.Active) return students.Where(_ => false);
		if (!hasClassTeacherHistory) return students;
		var assignments = (await classTeacherRepository.GetQueryableAsync()).Where(assignment =>
			assignment.TenantId == CurrentTenant.Id);
		return FinanceStudentScopeQueryBuilder.ApplyExactTeacherAssignments(students, assignments, employee.Id);
	}

	private async Task<bool> HasClassTeacherHistoryAsync(
		Guid userId, Employee? employee, CancellationToken cancellationToken)
	{
		var classTeacherRoleIds = (await roleRepository.GetQueryableAsync()).Where(role =>
			role.TenantId == CurrentTenant.Id &&
			(role.Name == "Class Teacher" || role.Name == "ClassTeacher"))
			.Select(role => role.Id);
		var users = await identityUsers.GetQueryableAsync();
		var hasClassTeacherRole = await AsyncExecuter.AnyAsync(users.Where(user =>
			user.Id == userId && user.TenantId == CurrentTenant.Id &&
			user.Roles.Any(membership => classTeacherRoleIds.Contains(membership.RoleId))),
			cancellationToken);
		if (hasClassTeacherRole || employee == null) return hasClassTeacherRole;
		bool hasTeacherAssignment;
		using (dataFilter.Disable<ISoftDelete>())
		{
			hasTeacherAssignment = await classTeacherRepository.AnyAsync(assignment =>
				assignment.TenantId == CurrentTenant.Id && assignment.TeacherId == employee.Id,
				cancellationToken);
		}
		return hasTeacherAssignment;
	}

	private async Task<Employee?> GetCurrentTenantEmployeeAsync(Guid userId, CancellationToken cancellationToken)
	{
		var employees = (await employeeRepository.GetQueryableAsync()).Where(employee =>
			employee.TenantId == CurrentTenant.Id && employee.UserId == userId);
		var matches = await AsyncExecuter.ToListAsync(employees.Take(2), cancellationToken);
		if (matches.Count > 1)
			throw new AbpAuthorizationException("Finance employee identity is ambiguous.");
		return matches.Count == 0 ? null : matches[0];
	}
	public async Task EnsureAcademicContextAccessibleAsync(
		Guid? academicYearId,
		Guid? boardId,
		Guid? educationMediumId,
		Guid? standardId,
		Guid? divisionId,
		CancellationToken cancellationToken = default)
	{
		if (await HasTenantWideYearAccessAsync(academicYearId, boardId, educationMediumId, standardId, divisionId, cancellationToken))
		{
			return;
		}

		var scopedStudents = await GetScopedStudentsQueryAsync();

		if (academicYearId.HasValue && academicYearId.Value != Guid.Empty)
		{
			scopedStudents = scopedStudents.Where(s => s.AcademicYearId == academicYearId.Value);
		}

		if (boardId.HasValue && boardId.Value != Guid.Empty)
		{
			scopedStudents = scopedStudents.Where(s => s.AcademicBoardId == boardId.Value);
		}

		if (educationMediumId.HasValue && educationMediumId.Value != Guid.Empty)
		{
			scopedStudents = scopedStudents.Where(s => s.AcademicMediumId == educationMediumId.Value);
		}

		if (standardId.HasValue && standardId.Value != Guid.Empty)
		{
			scopedStudents = scopedStudents.Where(s => s.StandardId == standardId.Value);
		}

		if (divisionId.HasValue && divisionId.Value != Guid.Empty)
		{
			scopedStudents = scopedStudents.Where(s => s.DivisionId == divisionId.Value);
		}

		if (!await AsyncExecuter.AnyAsync(scopedStudents, cancellationToken))
		{
			throw new AbpAuthorizationException("Access denied: requested academic context is outside your user scope.");
		}
	}

	/// <summary>
	/// Non-throwing variant of <see cref="EnsureAcademicContextAccessibleAsync"/>.
	/// Use for list endpoints that should return an empty result (not 403) when the
	/// requested context is outside the current user's scope.
	/// </summary>
	public async Task<bool> IsAcademicContextAccessibleAsync(
		Guid? academicYearId,
		Guid? boardId,
		Guid? educationMediumId,
		Guid? standardId,
		Guid? divisionId,
		CancellationToken cancellationToken = default)
	{
		if (await HasTenantWideYearAccessAsync(academicYearId, boardId, educationMediumId, standardId, divisionId, cancellationToken))
		{
			return true;
		}

		var scopedStudents = await GetScopedStudentsQueryAsync();

		if (academicYearId.HasValue && academicYearId.Value != Guid.Empty)
			scopedStudents = scopedStudents.Where(s => s.AcademicYearId == academicYearId.Value);

		if (boardId.HasValue && boardId.Value != Guid.Empty)
			scopedStudents = scopedStudents.Where(s => s.AcademicBoardId == boardId.Value);

		if (educationMediumId.HasValue && educationMediumId.Value != Guid.Empty)
			scopedStudents = scopedStudents.Where(s => s.AcademicMediumId == educationMediumId.Value);

		if (standardId.HasValue && standardId.Value != Guid.Empty)
			scopedStudents = scopedStudents.Where(s => s.StandardId == standardId.Value);

		if (divisionId.HasValue && divisionId.Value != Guid.Empty)
			scopedStudents = scopedStudents.Where(s => s.DivisionId == divisionId.Value);

		return await AsyncExecuter.AnyAsync(scopedStudents, cancellationToken);
	}

	private async Task<bool> HasTenantWideYearAccessAsync(
		Guid? academicYearId, Guid? boardId, Guid? educationMediumId, Guid? standardId, Guid? divisionId,
		CancellationToken cancellationToken)
	{
		// Year-level Finance setup must be possible before the first student is admitted.
		// More specific contexts and individual students still use the scoped student query.
		if (!academicYearId.HasValue || academicYearId.Value == Guid.Empty ||
			boardId.HasValue || educationMediumId.HasValue || standardId.HasValue || divisionId.HasValue)
		{
			return false;
		}

		if (!CurrentUser.Id.HasValue)
		{
			throw new AbpAuthorizationException("Current user context is required.");
		}
		await GetActiveIdentityAsync(CurrentUser.Id.Value);

		var scope = await userScopeService.GetUserScopeAsync(CurrentUser.Id.Value);
		if (!scope.HasTenantWideScope())
		{
			return false;
		}

		return await academicYearRepository.AnyAsync(
			year => year.Id == academicYearId.Value && year.TenantId == CurrentTenant.Id,
			cancellationToken);
	}

	public async Task<IQueryable<Guid>> GetScopedStudentIdsQueryAsync()
	{
		var query = await GetScopedStudentsQueryAsync();
		return query.Select(s => s.Id);
	}

	public async Task EnsureStudentAccessibleAsync(Guid studentId, CancellationToken cancellationToken = default)
	{
		var scopedStudents = await GetScopedStudentsQueryAsync();
		if (!await AsyncExecuter.AnyAsync(scopedStudents.Where(s => s.Id == studentId), cancellationToken))
		{
			throw new AbpAuthorizationException("Access denied: student is outside your user scope.");
		}
	}

	private async Task<IQueryable<Student>> GetScopedStudentsQueryAsync()
	{
		var currentUserId = CurrentUser.Id;
		if (!currentUserId.HasValue)
		{
			throw new AbpAuthorizationException("Current user context is required.");
		}

		var identity = await GetActiveIdentityAsync(currentUserId.Value);
		var scope = await userScopeService.GetUserScopeAsync(currentUserId.Value);
		var query = (await studentRepository.GetQueryableAsync()).Where(student => student.TenantId == CurrentTenant.Id);

		query = FinanceStudentScopeQueryBuilder.ApplyPayerScope(query, scope);
		return scope.IsAdmin ? query : await ApplyClassTeacherCapAsync(query, currentUserId.Value, identity, default);
	}
}
