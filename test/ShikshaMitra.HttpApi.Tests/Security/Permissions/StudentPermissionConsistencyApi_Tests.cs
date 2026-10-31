using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Security.Permissions;

public partial class PermissionConsistencyApi_Tests
{
	[Fact]
	public async Task Students_catalogue_contains_only_canonical_actions()
	{
		var (role, _) = await CreateTargetAsync();
		var permissions = await ReadAsync(Url("R", role));
		var studentActions = permissions.Where(item => item.Name.StartsWith("Students.", StringComparison.Ordinal))
			.Select(item => item.Name).Except(ShikshaMitraSubModulePermissions.All).ToArray();
		studentActions.ShouldNotBeEmpty();
		studentActions.ShouldAllBe(name => name.EndsWith(".View") || name.EndsWith(".Add") || name.EndsWith(".Delete"));
		studentActions.ShouldContain(ShikshaMitraSubmodulePermissions.Students.Records_Overrides.View);
		studentActions.ShouldNotContain("Students.Records.Overrides.Delete");
		studentActions.ShouldNotContain("Students.Records.GuardianAccess.Delete");
		studentActions.Length.ShouldBe(108);
		studentActions.ShouldContain(ShikshaMitraSubmodulePermissions.Students.Portal_ProfilePhoto.View);
		studentActions.ShouldContain(ShikshaMitraSubmodulePermissions.Students.Portal_ProfilePhoto.Add);
		studentActions.ShouldNotContain("Students.Portal.ProfilePhoto.Delete");
	}

	[Fact]
	public async Task Teacher_assignment_and_development_environment_do_not_grant_Student_actions()
	{
		Factory.Services.GetRequiredService<IHostEnvironment>().IsDevelopment().ShouldBeTrue();
		var (role, user) = await CreateTargetAsync();
		foreach (var permission in new[]
		{
			ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View,
			ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Add,
			ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Delete,
			ShikshaMitraSubmodulePermissions.Students.Attendance_MarkAttendance.Add,
			ShikshaMitraSubmodulePermissions.Students.Records_Changes.Add
		})
			await AssertRealCheckerAsync(user, role, permission, false);
		// Existing Academic assignment-derived behavior remains unchanged.
		await AssertRealCheckerAsync(user, role, "Academics", true);
	}

	[Theory]
	[InlineData(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View)]
	[InlineData(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Add)]
	[InlineData(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Delete)]
	[InlineData(ShikshaMitraSubmodulePermissions.Students.Portal_ProfilePhoto.View)]
	[InlineData(ShikshaMitraSubmodulePermissions.Students.Portal_ProfilePhoto.Add)]
	public async Task Student_actions_follow_exact_role_grants_user_overrides_and_revocation(string action)
	{
		var (role, user) = await CreateTargetAsync();
		await GrantActionVisibilityAsync(Url("R", role), action);
		await SetAsync(Url("R", role), (action, true));
		await AssertRealCheckerAsync(user, role, action, true);
		foreach (var other in new[] {
			ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View,
			ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Add,
			ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Delete,
			ShikshaMitraSubmodulePermissions.Students.Portal_ProfilePhoto.View,
			ShikshaMitraSubmodulePermissions.Students.Portal_ProfilePhoto.Add }.Where(name => name != action))
			await AssertRealCheckerAsync(user, role, other, false);
		await SetOverridesAsync(user, (action, "deny"));
		await AssertRealCheckerAsync(user, role, action, false);
		await SetOverridesAsync(user, (action, "inherit"));
		await AssertRealCheckerAsync(user, role, action, true);
		await SetAsync(Url("R", role), (action, false));
		await AssertRealCheckerAsync(user, role, action, false);
		await SetOverridesAsync(user, (action, "allow"));
		await AssertRealCheckerAsync(user, role, action, true);
		await SetOverridesAsync(user, (action, "inherit"));
		await AssertRealCheckerAsync(user, role, action, false);
	}

	[Fact]
	public async Task Portal_photo_grants_require_no_staff_navigation_and_reject_unsupported_delete_atomically()
	{
		var (role, user) = await CreateTargetAsync();
		var url = Url("R", role);
		var view = ShikshaMitraSubmodulePermissions.Students.Portal_ProfilePhoto.View;
		var add = ShikshaMitraSubmodulePermissions.Students.Portal_ProfilePhoto.Add;
		await SetAsync(url, (view, true));
		await AssertRealCheckerAsync(user, role, view, true);
		await AssertRealCheckerAsync(user, role, add, false);
		await AssertRealCheckerAsync(user, role, ShikshaMitraSubmodulePermissions.Students.Records_StudentList.Add, false);
		var grants = await ReadAsync(url);
		grants.Single(item => item.Name == ShikshaMitraModulePermissions.Students).IsGranted.ShouldBeFalse();
		grants.Single(item => item.Name == ShikshaMitraSubModulePermissions.Records).IsGranted.ShouldBeFalse();
		using var rejected = await Client.PutAsJsonAsync(url, new { permissions = new[] {
			new { name = add, isGranted = true }, new { name = "Students.Portal.ProfilePhoto.Delete", isGranted = true } } });
		rejected.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await rejected.Content.ReadAsStringAsync());
		await AssertRealCheckerAsync(user, role, add, false);
		await AssertRealCheckerAsync(user, role, view, true);
	}

	[Theory]
	[InlineData("Students.Records.Changes.Create", "Students.Records.Changes.Add")]
	[InlineData("Students.Admissions.Workflows.Approve", "Students.Admissions.Workflows.Add")]
	[InlineData("Students.Records.GuardianAccess.Manage", "Students.Records.GuardianAccess.Add")]
	public async Task Retired_Student_grants_do_not_promote_to_broader_canonical_actions(string retired, string canonical)
	{
		var (role, user) = await CreateTargetAsync();
		await GrantActionVisibilityAsync(Url("R", role), canonical);
		await InsertRetiredStudentPermissionAsync(role, user, retired, denied: false);
		await AssertRealCheckerAsync(user, role, canonical, false);
		await AssertRealCheckerAsync(user, role, retired, false);
		await SetAsync(Url("R", role), (canonical, true));
		await AssertRealCheckerAsync(user, role, canonical, true);
	}

	[Theory]
	[InlineData("Students.Admissions.Workflows.Approve", "Students.Admissions.Workflows.Add")]
	[InlineData("Students.Records.Settings.Publish", "Students.Records.Settings.Add")]
	[InlineData("Students.Records.Changes.Cancel", "Students.Records.Changes.Add")]
	[InlineData("Students.Records.Changes.Cancel", "Students.Records.Changes.Delete")]
	public async Task Retired_Student_denials_remain_effective_and_are_editable_through_canonical_overrides(string retired, string canonical)
	{
		var (role, user) = await CreateTargetAsync();
		await GrantActionVisibilityAsync(Url("R", role), canonical);
		await SetAsync(Url("R", role), (canonical, true));
		await InsertRetiredStudentPermissionAsync(role, user, retired, denied: true);
		await AssertOverrideAsync(user, canonical, "deny", false);
		await AssertRealCheckerAsync(user, role, canonical, false);
		await SetOverridesAsync(user, (canonical, "inherit"));
		await AssertOverrideAsync(user, canonical, "inherit", true);
		await AssertRealCheckerAsync(user, role, canonical, true);
	}

	[Theory]
	[InlineData("Students.Records.Transitions.Add", "Students.Records.Transitions.Delete")]
	[InlineData("Students.Records.Transitions.Delete", "Students.Records.Transitions.Add")]
	public async Task Clearing_one_split_legacy_denial_preserves_the_other_action(string edited, string preserved)
	{
		var (role, user) = await CreateTargetAsync();
		await GrantActionVisibilityAsync(Url("R", role), edited, preserved);
		await SetAsync(Url("R", role), (edited, true), (preserved, true));
		await InsertRetiredStudentPermissionAsync(role, user, "Students.Records.Transitions.Create", denied: true);
		await AssertOverrideAsync(user, edited, "deny", false);
		await AssertOverrideAsync(user, preserved, "deny", false);
		await SetOverridesAsync(user, (edited, "inherit"));
		await AssertOverrideAsync(user, edited, "inherit", true);
		await AssertOverrideAsync(user, preserved, "deny", false);
		await AssertRealCheckerAsync(user, role, preserved, false);
		await SetOverridesAsync(user, (preserved, "inherit"));
		await AssertRealCheckerAsync(user, role, preserved, true);
	}

	private async Task InsertRetiredStudentPermissionAsync(string role, Guid user, string permission, bool denied)
	{
		using var scope = Factory.Services.CreateScope();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId);
		using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		var db = await scope.ServiceProvider.GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
		if (denied) db.Set<UserPermissionDeny>().Add(new UserPermissionDeny(Guid.NewGuid(), tenantId, user, permission));
		else
		{
			db.Set<PermissionGrant>().Add(new PermissionGrant(Guid.NewGuid(), permission, "R", role, tenantId));
			db.Set<PermissionGrant>().Add(new PermissionGrant(Guid.NewGuid(), permission, "U", user.ToString(), tenantId));
		}
		await uow.CompleteAsync();
	}
}
