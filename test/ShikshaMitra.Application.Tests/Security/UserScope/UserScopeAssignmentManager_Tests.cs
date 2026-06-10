using System;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Guids;
using Volo.Abp;
using Volo.Abp.Identity;
using Xunit;

namespace ShikshaMitra.Security.UserScope;

public class UserScopeAssignmentManager_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private readonly UserScopeAssignmentManager _manager;
	private readonly IIdentityUserRepository _userRepository;
	private readonly IGuidGenerator _guidGenerator;

	public UserScopeAssignmentManager_Tests()
	{
		_manager = GetRequiredService<UserScopeAssignmentManager>();
		_userRepository = GetRequiredService<IIdentityUserRepository>();
		_guidGenerator = GetRequiredService<IGuidGenerator>();
	}

	[Fact]
	public async Task CreateAsync_Should_Reject_Missing_Target()
	{
		var exception = await Should.ThrowAsync<BusinessException>(() =>
			_manager.CreateAsync(userId: null, roleName: null));

		exception.Code.ShouldBe(UserScopeErrorCodes.InvalidAssignment);
	}

	[Fact]
	public async Task CreateAsync_Should_Reject_Unknown_User()
	{
		var exception = await Should.ThrowAsync<BusinessException>(() =>
			_manager.CreateAsync(userId: Guid.NewGuid(), roleName: null));

		exception.Code.ShouldBe(UserScopeErrorCodes.UserNotFound);
	}

	[Fact]
	public async Task CreateAsync_Should_Reject_Unknown_Role()
	{
		var exception = await Should.ThrowAsync<BusinessException>(() =>
			_manager.CreateAsync(userId: null, roleName: $"missing-role-{Guid.NewGuid():N}"));

		exception.Code.ShouldBe(UserScopeErrorCodes.RoleNotFound);
	}

	[Fact]
	public async Task CreateAsync_Should_Reject_Unknown_Hierarchy_Reference()
	{
		var user = new IdentityUser(
			_guidGenerator.Create(),
			$"scope-user-{Guid.NewGuid():N}",
			$"scope-user-{Guid.NewGuid():N}@example.com");
		await _userRepository.InsertAsync(user, autoSave: true);

		var exception = await Should.ThrowAsync<BusinessException>(() =>
			_manager.CreateAsync(
				userId: user.Id,
				roleName: null,
				academicYearId: Guid.NewGuid()));

		exception.Code.ShouldBe(UserScopeErrorCodes.InvalidReferences);
	}
}
