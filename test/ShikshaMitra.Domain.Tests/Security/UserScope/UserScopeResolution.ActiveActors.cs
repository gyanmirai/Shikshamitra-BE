using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Security.UserScope;

public partial class UserScopeResolution_Tests
{
	[Fact]
	public async Task Bulk_resolution_should_deny_an_inactive_root_even_when_its_role_and_assignments_remain()
	{
		_repository.GetActiveUserIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(new List<Guid>());
		_repository.GetRoleNamesByUserIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
			.Returns(new Dictionary<Guid, List<string>> { [_user] = ["admin"] });

		var result = await _service.GetUserScopesAsync([_user]);

		result[_user].HasAnyAccess.ShouldBeFalse(); result[_user].IsAdmin.ShouldBeFalse();
	}

	[Fact]
	public async Task Bulk_resolution_should_preserve_active_manager_inheritance_from_a_disabled_login_reportee()
	{
		var reporteeUser = Guid.NewGuid(); var managerEmployee = Guid.NewGuid();
		var stage = Guid.NewGuid();
		_repository.GetActiveUserIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
			.Returns(call => call.ArgAt<IReadOnlyCollection<Guid>>(0).Where(id => id == _user).ToList());
		_repository.GetReportingHierarchyAsync(Arg.Any<CancellationToken>()).Returns(new List<ScopeReportingEmployee>
			{ new(managerEmployee, _user, null), new(Guid.NewGuid(), reporteeUser, managerEmployee) });
		_repository.GetRoleNamesByUserIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
			.Returns(new Dictionary<Guid, List<string>> { [_user] = [], [reporteeUser] = ["teacher"] });
		_repository.GetAssignmentsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
			.Returns(new List<UserScopeAssignment> { new(Guid.NewGuid(), _tenantId, roleName: "teacher", educationMediumStageId: stage) });

		var result = await _service.GetUserScopesAsync([_user, reporteeUser]);

		result[_user].MediumStageIds.ShouldContain(stage);
		result[reporteeUser].HasAnyAccess.ShouldBeFalse();
		await _repository.Received(1).GetActiveUserIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
	}
}
