using System;
using System.Text.Json;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Setting.GeneralSetting.LibraryTimings;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;
using Xunit;

namespace ShikshaMitra.Library;

public sealed class LibraryHoursCapabilities_Tests : ShikshaMitraApplicationTestBase<LibraryReceivingPreviewTestModule>
{
	private ILibraryTimingAppService Hours => GetRequiredService<ILibraryTimingAppService>();
	private IPermissionChecker Permissions => GetRequiredService<IPermissionChecker>();

	public LibraryHoursCapabilities_Tests()
	{
		Permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Settings_LibraryHours.View).Returns(true);
		Permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Settings_LibraryHours.Add).Returns(true);
	}

	[Fact]
	public async Task Hours_Capability_Should_Allow_Actual_Schoolwide_Add_Without_Calendar_Management()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await WithUnitOfWorkAsync(() => LibraryPolicyTestFixture.SeedAsync(ServiceProvider));
		Permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Settings_BorrowingRules.Add).Returns(false);
		var result = await Hours.GetListAsync(context.YearId);
		result.TotalCount.ShouldBe(7);
		Capability(result).ShouldBeTrue();
		await Hours.UpdateHoursAsync(context.YearId, Request());
		(await Hours.GetListAsync(context.YearId)).Items.ShouldContain(row => row.Day == "Monday" && row.StartTime == "11:45:00");
	}

	[Fact]
	public async Task Hours_Capability_Should_Keep_View_Only_Rows_Without_Offering_Add()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await WithUnitOfWorkAsync(() => LibraryPolicyTestFixture.SeedAsync(ServiceProvider));
		Permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Settings_LibraryHours.Add).Returns(false);
		var result = await Hours.GetListAsync(context.YearId);
		result.TotalCount.ShouldBe(7);
		Capability(result).ShouldBeFalse();
		await Should.ThrowAsync<AbpAuthorizationException>(() => Hours.UpdateHoursAsync(context.YearId, Request()));
	}

	[Fact]
	public async Task Hours_Capability_Should_Keep_Authorized_Year_Read_While_Denying_Global_Changes()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await WithUnitOfWorkAsync(() => LibraryPolicyTestFixture.SeedAsync(ServiceProvider));
		await ReplaceScopeAsync(context.YearId);
		(await GetRequiredService<ShikshaMitra.Library.Shared.ILibraryAccessService>().HasTenantWideScopeAsync()).ShouldBeFalse();
		var result = await Hours.GetListAsync(context.YearId);
		result.TotalCount.ShouldBe(7);
		Capability(result).ShouldBeFalse();
		(await Should.ThrowAsync<BusinessException>(() => Hours.UpdateHoursAsync(context.YearId, Request())))
			.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
	}

	[Fact]
	public async Task Hours_Capability_Read_Should_Recheck_Current_View_Action()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await WithUnitOfWorkAsync(() => LibraryPolicyTestFixture.SeedAsync(ServiceProvider));
		(await Hours.GetListAsync(context.YearId)).TotalCount.ShouldBe(7);
		Permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Settings_LibraryHours.View).Returns(false);
		await Should.ThrowAsync<AbpAuthorizationException>(() => Hours.GetListAsync(context.YearId));
	}

	[Fact]
	public async Task Hours_Capability_Read_Should_Not_Convert_Revoked_Scope_Into_An_Empty_List()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await WithUnitOfWorkAsync(() => LibraryPolicyTestFixture.SeedAsync(ServiceProvider));
		(await Hours.GetListAsync(context.YearId)).TotalCount.ShouldBe(7);
		await ReplaceScopeAsync(null);
		(await Should.ThrowAsync<BusinessException>(() => Hours.GetListAsync(context.YearId)))
			.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
	}

	private static bool Capability(object result)
	{
		var json = JsonSerializer.SerializeToElement(result, result.GetType(), new JsonSerializerOptions(JsonSerializerDefaults.Web));
		json.TryGetProperty("canManageHours", out var capability).ShouldBeTrue("The Hours read contract must expose its own current management decision.");
		return capability.GetBoolean();
	}

	private static LibraryHoursUpdateRequest Request() => new()
	{
		Hours = [new() { Day = "Monday", Status = 0, StartTime = "11:45:00", EndTime = "15:00:00" }]
	};

	private async Task ReplaceScopeAsync(Guid? yearId)
	{
		var actor = GetRequiredService<ICurrentUser>().Id!.Value;
		var tenant = GetRequiredService<ICurrentTenant>().Id;
		await WithUnitOfWorkAsync(async () =>
		{
			var assignments = GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
			await assignments.DeleteAsync(row => row.TenantId == tenant && row.UserId == actor, true);
			if (yearId.HasValue)
				await assignments.InsertAsync(new(Guid.NewGuid(), tenant, userId: actor, academicYearId: yearId.Value), true);
		});
		await GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(actor);
	}
}
