using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.Seeders;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.Data;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.Seeders;

public class SecurityPermissionSeeder_Tests
{
	[Theory]
	[InlineData("Clerk")]
	[InlineData("Office Assistant")]
	public void Fresh_Office_Roles_Receive_Only_Hr_Leave_Self_Service(string role)
	{
		var permissions = RolePermissionSeederContributor.GetInitialPermissions(role, Guid.NewGuid());
		var expected = new[]
		{
			ShikshaMitraSubmodulePermissions.HR.Default,
			ShikshaMitraSubmodulePermissions.HR.LeaveManagement_LeaveCenter.View,
			ShikshaMitraSubmodulePermissions.HR.LeaveManagement_LeaveCenter.Add,
			ShikshaMitraSubmodulePermissions.HR.LeaveManagement_LeaveCenter.Delete
		};
		Assert.Equal(expected.Order(), permissions.Where(name => name == "HR" || name.StartsWith("HR.", StringComparison.Ordinal)).Order());
		Assert.Contains(ShikshaMitraModulePermissions.HR, permissions);
		Assert.Contains(ShikshaMitraSubModulePermissions.LeaveManagement, permissions);
	}

	[Theory]
	[InlineData("FullDemo")]
	[InlineData("Minimal")]
	[InlineData("FastDemo")]
	public async Task Rerunning_Permission_Seeder_Must_Not_Restore_Missing_Grants(string profile)
	{
		var tenant = Substitute.For<ICurrentTenant>();
		tenant.Id.Returns(Guid.NewGuid());
		var roles = Substitute.For<IIdentityRoleRepository>();
		var existingRoles = RolePermissionSeederContributor.BuildRolePermissions().Keys
			.Select(name => new IdentityRole(Guid.NewGuid(), name, tenant.Id)).ToList();
		roles.GetListAsync().Returns(existingRoles);
		var grants = Substitute.For<IPermissionGrantSeedRepository>();
		var seeder = new RolePermissionSeederContributor(tenant, Substitute.For<IGuidGenerator>(),
			Substitute.For<IUnitOfWork>(), roles, grants);
		var context = new DataSeedContext(tenant.Id).WithProperty("Profile", profile);

		await seeder.SeedAsync(context);
		await seeder.SeedAsync(context);

		await grants.DidNotReceiveWithAnyArgs().InsertRoleGrantsIgnoringDuplicatesAsync(
			default!, default!, default, default);
		await grants.DidNotReceiveWithAnyArgs().MirrorRoleGrantsIgnoringDuplicatesAsync(
			default!, default, default);
	}

	[Fact]
	public async Task Defaults_Are_Granted_Only_In_The_New_Role_Creation_Path()
	{
		var tenant = Substitute.For<ICurrentTenant>();
		var tenantId = Guid.NewGuid();
		tenant.Id.Returns(tenantId);
		var roles = Substitute.For<IIdentityRoleRepository>();
		var existing = new Dictionary<string, IdentityRole>();
		roles.FindByNormalizedNameAsync(Arg.Any<string>()).Returns(call =>
			existing.GetValueOrDefault(call.ArgAt<string>(0)));
		roles.GetListAsync().Returns(_ => existing.Values.ToList());
		roles.InsertAsync(Arg.Any<IdentityRole>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
			.Returns(call =>
			{
				var role = call.ArgAt<IdentityRole>(0);
				existing.Add(role.NormalizedName, role);
				return role;
			});
		var grants = Substitute.For<IPermissionGrantSeedRepository>();
		var guids = Substitute.For<IGuidGenerator>();
		guids.Create().Returns(_ => Guid.NewGuid());
		var seeder = new RolesSeederContributor(tenant, guids, Substitute.For<IUnitOfWork>(), roles, grants);
		await seeder.SeedAsync(new DataSeedContext(tenantId));
		await grants.Received().InsertRoleGrantsIgnoringDuplicatesAsync("HR Manager",
			Arg.Is<IReadOnlyCollection<string>>(x => x.Contains("HR.PayrollManagement.RunPayroll.Lock")),
			tenantId, Arg.Any<CancellationToken>());

		grants.ClearReceivedCalls();
		await seeder.SeedAsync(new DataSeedContext(tenantId));
		await grants.DidNotReceiveWithAnyArgs().InsertRoleGrantsIgnoringDuplicatesAsync(default!, default!, default, default);
	}
}
