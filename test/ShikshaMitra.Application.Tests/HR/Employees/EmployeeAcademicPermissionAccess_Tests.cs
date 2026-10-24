using System;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.HR.Employees.AcademicPermissions;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp.Authorization;
using Xunit;

namespace ShikshaMitra.HR.Employees;

public class EmployeeAcademicPermissionAccess_Tests
{
	[Fact]
	public async Task Direct_Read_Enforces_Employee_Access_Before_Reading_Identity_Scope()
	{
		var authorization = Substitute.For<IEmployeeAuthorizationService>();
		var employeeId = Guid.NewGuid();
		authorization.CheckEmployeeAccessAsync(employeeId)
			.Returns(Task.FromException(new AbpAuthorizationException("Denied employee access")));
		var employees = Substitute.For<IEmployeeRepository>();
		var service = new EmployeeAcademicPermissionAppService(employees, authorization, null!, null!, null!, null!, null!);

		await Should.ThrowAsync<AbpAuthorizationException>(() => service.GetAsync(employeeId, Guid.NewGuid()));
		employees.ReceivedCalls().ShouldBeEmpty();
	}

	[Fact]
	public async Task Direct_Write_Enforces_Employee_Modify_Access_Before_Changing_Scopes()
	{
		var authorization = Substitute.For<IEmployeeAuthorizationService>();
		var employeeId = Guid.NewGuid();
		authorization.CheckEmployeeModifyAccessAsync(employeeId)
			.Returns(Task.FromException(new AbpAuthorizationException("Denied employee write")));
		var employees = Substitute.For<IEmployeeRepository>();
		var service = new EmployeeAcademicPermissionAppService(employees, authorization, null!, null!, null!, null!, null!);

		await Should.ThrowAsync<AbpAuthorizationException>(() => service.UpdateAsync(employeeId, new BulkScopeAssignmentDto()));
		employees.ReceivedCalls().ShouldBeEmpty();
	}
}
