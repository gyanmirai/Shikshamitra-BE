using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Library.Members;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students;

namespace ShikshaMitra.Library.Shared;

public interface ILibraryAccessService
{
    Task<bool> HasTenantWideScopeAsync();
    Task<LibraryAcademicContext> GetContextAsync(Guid? academicYearId = null);
    Task<IQueryable<Student>> GetStudentsAsync(UserScopeContext? scope = null);
    Task<IQueryable<Employee>> GetEmployeesAsync(UserScopeContext? scope = null);
    Task<HashSet<Guid>> GetMemberIdsAsync(UserScopeContext? scope = null);
    Task<List<LibraryMember>> GetMembershipsAsync(UserScopeContext? scope = null);
    Task<Guid> EnsureMemberAsync(Guid memberId);
    Task<List<Guid>> GetPersonMemberIdsAsync(Guid memberId);
}
