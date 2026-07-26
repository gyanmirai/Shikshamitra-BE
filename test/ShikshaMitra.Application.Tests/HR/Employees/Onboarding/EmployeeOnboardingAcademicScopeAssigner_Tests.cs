using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.Security.Permissions;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Xunit;

namespace ShikshaMitra.HR.Employees.Onboarding;

public class EmployeeOnboardingAcademicScopeAssigner_Tests
{
	[Fact]
	public async Task ValidateAsync_Should_Require_Bulk_Assign_Permission_Before_Hierarchy_Access()
	{
		var permissionChecker = Substitute.For<IPermissionChecker>();
		permissionChecker
			.IsGrantedAsync(ShikshaMitraPermissions.UserScope.BulkAssign)
			.Returns(false);
		var assigner = new EmployeeOnboardingAcademicScopeAssigner(
			null!,
			null!,
			null!,
			null!,
			permissionChecker);

		var exception = await Should.ThrowAsync<AbpAuthorizationException>(() =>
			assigner.ValidateAsync(
				Guid.NewGuid(),
				new List<EmployeeApplicationAcademicScopeDto>
				{
					new() { EducationBoardId = Guid.NewGuid() }
				}));

		exception.Message.ShouldContain(ShikshaMitraPermissions.UserScope.BulkAssign);
		await permissionChecker.Received(1)
			.IsGrantedAsync(ShikshaMitraPermissions.UserScope.BulkAssign);
	}
}
