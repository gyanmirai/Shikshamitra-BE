using System;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Employees.AcademicPermissions;

public interface IEmployeeAcademicPermissionAppService : IApplicationService
{
	Task<UserScopeHierarchyDto> GetAsync(Guid employeeId, Guid academicYearId);
	Task<BulkScopeAssignmentResultDto> UpdateAsync(Guid employeeId, BulkScopeAssignmentDto input);
}
