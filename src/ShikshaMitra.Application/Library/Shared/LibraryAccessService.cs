using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Library.Members;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Library.Shared;

[RemoteService(false)]
public class LibraryAccessService(
    IUserScopeAccessService scopeAccess,
    IStudentRepository students,
    IRepository<Employee, Guid> employees,
    IRepository<LibraryMember, Guid> memberships,
    HrEmployeeScopeQueryService employeeScopes,
    IRepository<AcademicYear, Guid> academicYears)
    : ApplicationService, ILibraryAccessService, ITransientDependency
{
    public async Task<bool> HasTenantWideScopeAsync() =>
        (await scopeAccess.GetCurrentUserScopeAsync()).HasTenantWideScope();

    public async Task<LibraryAcademicContext> GetContextAsync(Guid? academicYearId = null)
    {
        var scope = await scopeAccess.GetCurrentUserScopeAsync();
        scopeAccess.EnsureHasAnyAccess(scope, UserScopeErrorCodes.AccessDenied);
        var years = await academicYears.GetListAsync(x => x.TenantId == CurrentTenant.Id);
        var allowed = years.Where(x => scope.HasTenantWideScope() ||
            scope.AcademicYearIds.Contains(x.Id) || scope.WildcardBoardAcademicYearIds.Contains(x.Id))
            .Select(x => x.Id).ToList();
        if (scope.TenantId != CurrentTenant.Id || allowed.Count == 0 ||
            academicYearId.HasValue && !allowed.Contains(academicYearId.Value))
            throw new BusinessException(UserScopeErrorCodes.AccessDenied);
        return new LibraryAcademicContext(scope, allowed);
    }

    public async Task<IQueryable<Student>> GetStudentsAsync(UserScopeContext? scope = null)
    {
        scope ??= await scopeAccess.GetCurrentUserScopeAsync();
        var query = (await students.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id);
        return scope.TenantId == CurrentTenant.Id
            ? StudentScopeAccessService.ApplyScope(query, scope)
            : query.Where(_ => false);
    }

    public async Task<IQueryable<Employee>> GetEmployeesAsync(UserScopeContext? scope = null)
    {
        scope ??= await scopeAccess.GetCurrentUserScopeAsync();
        var query = (await employees.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id);
        if (scope.TenantId != CurrentTenant.Id || !HrUserScopeAccess.HasAnyConcreteHrStageAccess(scope))
            return query.Where(_ => false);

        var scoped = await employeeScopes.BuildScopedEmployeeQueryAsync(scope);
        return scoped.Where(x => x.TenantId == CurrentTenant.Id);
    }

    public async Task<HashSet<Guid>> GetMemberIdsAsync(UserScopeContext? scope = null)
    {
        scope ??= await scopeAccess.GetCurrentUserScopeAsync();
        var studentIds = await AsyncExecuter.ToListAsync((await GetStudentsAsync(scope)).Select(x => x.Id));
        var employeeIds = await AsyncExecuter.ToListAsync((await GetEmployeesAsync(scope)).Select(x => x.Id));
        var aliases = await AsyncExecuter.ToListAsync((await memberships.GetQueryableAsync())
            .Where(x => x.TenantId == CurrentTenant.Id && studentIds.Contains(x.StudentId)).Select(x => x.Id));
        return studentIds.Concat(employeeIds).Concat(aliases).ToHashSet();
    }

    public async Task<List<LibraryMember>> GetMembershipsAsync(UserScopeContext? scope = null)
    {
        var studentIds = (await GetStudentsAsync(scope)).Select(x => x.Id);
        return await AsyncExecuter.ToListAsync((await memberships.GetQueryableAsync())
            .Where(x => x.TenantId == CurrentTenant.Id && studentIds.Contains(x.StudentId)));
    }

    public async Task<Guid> EnsureMemberAsync(Guid memberId)
    {
        var student = await students.FindAsync(memberId);
        if (student == null)
        {
            var membership = await memberships.FindAsync(memberId);
            if (membership != null) student = await students.FindAsync(membership.StudentId);
        }

        if (student != null)
        {
            var readable = await GetStudentsAsync();
            if (!await AsyncExecuter.AnyAsync(readable.Where(x => x.Id == student.Id)))
                throw new BusinessException(UserScopeErrorCodes.AccessDenied);
            return student.Id;
        }

        var employee = await employees.FindAsync(memberId);
        if (employee == null)
            throw new BusinessException(LibraryErrorCodes.MemberNotFound, "Library member was not found.");

        var readableEmployees = await GetEmployeesAsync();
        if (!await AsyncExecuter.AnyAsync(readableEmployees.Where(x => x.Id == employee.Id)))
            throw new BusinessException(UserScopeErrorCodes.AccessDenied);
        return employee.Id;
    }

    public async Task<List<Guid>> GetPersonMemberIdsAsync(Guid memberId)
    {
        var personId = await EnsureMemberAsync(memberId);
        var aliases = await AsyncExecuter.ToListAsync((await memberships.GetQueryableAsync())
            .Where(x => x.TenantId == CurrentTenant.Id && x.StudentId == personId).Select(x => x.Id));
        return aliases.Append(personId).Distinct().ToList();
    }
}
