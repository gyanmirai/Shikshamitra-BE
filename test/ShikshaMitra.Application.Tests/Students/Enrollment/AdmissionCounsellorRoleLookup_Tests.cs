using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using ShikshaMitra.Security.UserScope;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public async Task Counsellor_role_lookup_should_filter_before_limit_and_preserve_default_staff_lookup()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var suffix = Guid.NewGuid().ToString("N");
		var prefix = $"role-lookup-{suffix}-";
		var counsellorId = Guid.NewGuid();
		await WithUnitOfWorkAsync(async () =>
		{
			var users = GetRequiredService<IRepository<IdentityUser, Guid>>();
			var roles = GetRequiredService<IRepository<IdentityRole, Guid>>();
			var role = await roles.FindAsync(item => item.NormalizedName == "ADMISSIONS COUNSELOR") ??
				await roles.InsertAsync(new IdentityRole(Guid.NewGuid(), "Admissions Counselor", TestTenantId));
			var assignments = GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
			for (var index = 0; index < 80; index++)
			{
				var id = index == 79 ? counsellorId : Guid.NewGuid();
				var user = new IdentityUser(id, $"{prefix}{index:000}", $"{id:N}@example.test", TestTenantId);
				if (id == counsellorId) user.AddRole(role.Id);
				await users.InsertAsync(user);
				await assignments.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId, userId: id));
			}
		});
		var service = GetRequiredService<IAdmissionIntakeProfileAppService>();
		var staff = await service.GetCounsellorsAsync(prefix);
		staff.Count.ShouldBe(50);
		staff.ShouldNotContain(item => item.Id == counsellorId);
		var counsellors = await service.GetCounsellorsAsync(prefix, counsellorOnly: true);
		counsellors.Single().Id.ShouldBe(counsellorId);
	}

	[Fact]
	public async Task Counsellor_role_lookup_should_require_active_identity_scope_and_current_role_membership()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var prefix = $"role-state-{Guid.NewGuid():N}-";
		var activeId = Guid.NewGuid();
		var inactiveId = Guid.NewGuid();
		var unscopedId = Guid.NewGuid();
		var nonCounsellorId = Guid.NewGuid();
		var roles = GetRequiredService<IRepository<IdentityRole, Guid>>();
		Guid counsellorRoleId = Guid.Empty;
		await WithUnitOfWorkAsync(async () =>
		{
			var role = await roles.FindAsync(item => item.NormalizedName == "ADMISSIONS COUNSELOR") ??
				await roles.InsertAsync(new IdentityRole(Guid.NewGuid(), "Admissions Counselor", TestTenantId));
			counsellorRoleId = role.Id;
			var users = GetRequiredService<IRepository<IdentityUser, Guid>>();
			var assignments = GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
			foreach (var id in new[] { activeId, inactiveId, unscopedId, nonCounsellorId })
			{
				var user = new IdentityUser(id, $"{prefix}{id:N}", $"{id:N}@example.test", TestTenantId);
				if (id != nonCounsellorId) user.AddRole(role.Id);
				user.SetIsActive(id != inactiveId);
				await users.InsertAsync(user);
				if (id != unscopedId)
					await assignments.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId, userId: id));
			}
		});
		var service = GetRequiredService<IAdmissionIntakeProfileAppService>();
		(await service.GetCounsellorsAsync(prefix, counsellorOnly: true)).Single().Id.ShouldBe(activeId);
		var broad = await service.GetCounsellorsAsync(prefix);
		broad.Select(item => item.Id).OrderBy(id => id).ShouldBe(new[] { activeId, nonCounsellorId }.OrderBy(id => id));
		await WithUnitOfWorkAsync(async () =>
		{
			var users = GetRequiredService<IRepository<IdentityUser, Guid>>();
			var user = await users.GetAsync(activeId, includeDetails: true);
			user.RemoveRole(counsellorRoleId);
			await users.UpdateAsync(user, true);
		});
		(await service.GetCounsellorsAsync(prefix, counsellorOnly: true)).ShouldBeEmpty();
	}
}
