using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ShikshaMitra.Constants.Permissions;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Users;
using Xunit;

namespace ShikshaMitra.Students.Setup;

public class StudentSetupPermissionBatch_Tests
{
	private const string HouseView = ShikshaMitraSubmodulePermissions.Students.Records_HouseManagement.View;
	private const string HouseManage = ShikshaMitraSubmodulePermissions.Students.Records_HouseManagement.Add;
	private const string FormManage = ShikshaMitraSubmodulePermissions.Students.AdmissionForms.Add;

	[Fact]
	public async Task Full_setting_permissions_share_one_distinct_batch_and_keep_manage_dependent_on_view()
	{
		using var fixture = new Fixture(HouseView, HouseManage, FormManage);
		var rows = await fixture.Access.ReadPermissionsAsync();
		rows.Count.ShouldBe(9);
		rows.Single(row => row.Key == "houses").CanView.ShouldBeTrue();
		rows.Single(row => row.Key == "houses").CanManage.ShouldBeTrue();
		rows.Single(row => row.Key == "admission-forms").CanView.ShouldBeFalse();
		rows.Single(row => row.Key == "admission-forms").CanManage.ShouldBeFalse();
		fixture.Batches.Count.ShouldBe(1);
		fixture.Batches[0].Length.ShouldBe(14);
		fixture.Batches[0].Distinct(StringComparer.Ordinal).Count().ShouldBe(14);
		fixture.SingleChecks.ShouldBeEmpty();
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Visit_status_batches_view_only_and_preserves_the_saved_marker_without_writing(bool visited)
	{
		using var fixture = new Fixture(HouseView, FormManage);
		var savedAt = new DateTime(2026, 11, 1, 9, 30, 0, DateTimeKind.Utc);
		if (visited) StudentSetupVisit.Record(fixture.Tenant, fixture.ActorId, savedAt);
		var result = await fixture.Visits.GetAsync();
		result.CanAccessSetup.ShouldBeTrue();
		result.HasVisitedConfiguration.ShouldBe(visited);
		result.FirstVisitedAt.ShouldBe(visited ? savedAt : null);
		fixture.Batches.Count.ShouldBe(1);
		fixture.Batches[0].Length.ShouldBe(7);
		fixture.Batches[0].Distinct(StringComparer.Ordinal).Count().ShouldBe(7);
		fixture.Batches[0].ShouldNotContain(FormManage);
		fixture.Batches[0].ShouldNotContain(HouseManage);
		fixture.SingleChecks.ShouldBeEmpty();
		fixture.Tenants.ReceivedCalls().ShouldHaveSingleItem()
			.GetMethodInfo().Name.ShouldBe("GetAsync");
	}

	[Fact]
	public async Task Manage_without_a_setup_view_does_not_disclose_the_tenant_visit_marker()
	{
		using var fixture = new Fixture(HouseManage);
		var result = await fixture.Visits.GetAsync();
		result.CanAccessSetup.ShouldBeFalse();
		result.HasVisitedConfiguration.ShouldBeFalse();
		result.FirstVisitedAt.ShouldBeNull();
		fixture.Tenants.ReceivedCalls().ShouldBeEmpty();
		fixture.Batches.Count.ShouldBe(1);
		fixture.SingleChecks.ShouldBeEmpty();
	}

	[Theory]
	[InlineData(PermissionGrantResult.Prohibited)]
	[InlineData(PermissionGrantResult.Undefined)]
	public async Task Only_explicitly_granted_batch_results_allow_setup(PermissionGrantResult grant)
	{
		using var fixture = new Fixture();
		fixture.PermissionResults[HouseView] = grant;
		(await fixture.Visits.GetAsync()).CanAccessSetup.ShouldBeFalse();
		fixture.Tenants.ReceivedCalls().ShouldBeEmpty();
		fixture.Batches.Count.ShouldBe(1);
	}

	[Fact]
	public async Task Batch_check_failure_propagates_without_reading_the_tenant()
	{
		using var fixture = new Fixture();
		fixture.Checker.IsGrantedAsync(Arg.Any<string[]>())
			.Returns(Task.FromException<MultiplePermissionGrantResult>(new InvalidOperationException("permission lookup failed")));
		await Should.ThrowAsync<InvalidOperationException>(() => fixture.Visits.GetAsync());
		fixture.Tenants.ReceivedCalls().ShouldBeEmpty();
	}

	[Theory]
	[InlineData("anonymous")]
	[InlineData("foreign-tenant")]
	[InlineData("inactive")]
	public async Task Invalid_identity_is_rejected_before_any_permission_or_tenant_read(string scenario)
	{
		using var fixture = new Fixture(HouseView);
		if (scenario == "anonymous") fixture.User.IsAuthenticated.Returns(false);
		if (scenario == "foreign-tenant") fixture.User.TenantId.Returns(Guid.NewGuid());
		if (scenario == "inactive") fixture.Actor.SetIsActive(false);
		await Should.ThrowAsync<AbpAuthorizationException>(() => fixture.Visits.GetAsync());
		fixture.Batches.ShouldBeEmpty();
		fixture.SingleChecks.ShouldBeEmpty();
		fixture.Tenants.ReceivedCalls().ShouldBeEmpty();
	}

	private sealed class Fixture : IDisposable
	{
		private readonly ServiceProvider services;
		public Guid ActorId { get; } = Guid.NewGuid();
		public IdentityUser Actor { get; }
		public ICurrentUser User { get; } = Substitute.For<ICurrentUser>();
		public IPermissionChecker Checker { get; } = Substitute.For<IPermissionChecker>();
		public ITenantRepository Tenants { get; } = Substitute.For<ITenantRepository>();
		public Dictionary<string, PermissionGrantResult> PermissionResults { get; } = new(StringComparer.Ordinal);
		public List<string[]> Batches { get; } = [];
		public List<string> SingleChecks { get; } = [];
		public Tenant Tenant { get; } = new TestTenant(Guid.NewGuid());
		public StudentSetupAccess Access { get; }
		public StudentSetupVisitService Visits { get; }

		public Fixture(params string[] grants)
		{
			foreach (var grant in grants) PermissionResults[grant] = PermissionGrantResult.Granted;
			Checker.IsGrantedAsync(Arg.Any<string>()).Returns(call => {
				var name = call.Arg<string>(); SingleChecks.Add(name);
				return Task.FromResult(PermissionResults.GetValueOrDefault(name) == PermissionGrantResult.Granted);
			});
			Checker.IsGrantedAsync(Arg.Any<string[]>()).Returns(call => {
				var names = call.Arg<string[]>(); Batches.Add(names.ToArray());
				var result = new MultiplePermissionGrantResult(names);
				foreach (var name in names) result.Result[name] = PermissionResults.GetValueOrDefault(name, PermissionGrantResult.Prohibited);
				return Task.FromResult(result);
			});
			Actor = new IdentityUser(ActorId, "setup-batch", "setup-batch@example.test", Tenant.Id);
			User.Id.Returns(ActorId); User.TenantId.Returns(Tenant.Id); User.IsAuthenticated.Returns(true);
			var tenant = Substitute.For<ICurrentTenant>(); tenant.Id.Returns(Tenant.Id);
			var users = Substitute.For<IIdentityUserRepository>();
			users.FindAsync(ActorId, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<IdentityUser?>(Actor));
			Tenants.GetAsync(Tenant.Id, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(Tenant));
			Tenants.ClearReceivedCalls();
			services = new ServiceCollection().AddSingleton(User).AddSingleton(tenant).BuildServiceProvider();
			Access = new StudentSetupAccess(users, null!, null!, null!, null!, null!, null!, Checker) {
				LazyServiceProvider = new AbpLazyServiceProvider(services)
			};
			Visits = new StudentSetupVisitService(Access, Tenants, null!);
		}

		public void Dispose() => services.Dispose();
	}

	private sealed class TestTenant(Guid id) : Tenant(id, "Setup batch test", "SETUP BATCH TEST");
}
