using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Security.Roles;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Security;

public class RoleDeletionTestFactory : ShikshaMitraApiTestFactory
{
	protected override bool InitializeTestData => false;

	public async Task<(Guid TenantId, Guid RoleId)> SeedAsync(int users = 0, bool employee = false, bool archived = false)
	{
		using var scope = Services.CreateScope();
		var services = scope.ServiceProvider;
		using var host = services.GetRequiredService<ICurrentTenant>().Change(null);
		using var unit = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var tenant = await services.GetRequiredService<ITenantManager>().CreateAsync("role-delete-" + Guid.NewGuid().ToString("N"));
		await services.GetRequiredService<IRepository<Tenant, Guid>>().InsertAsync(tenant);
		using var currentTenant = services.GetRequiredService<ICurrentTenant>().Change(tenant.Id);
		var role = new IdentityRole(Guid.NewGuid(), "Intern Teacher", tenant.Id);
		await services.GetRequiredService<IIdentityRoleRepository>().InsertAsync(role);
		Guid? linkedUser = null;
		for (var i = 0; i < users; i++)
		{
			var id = Guid.NewGuid();
			var user = new IdentityUser(id, "qa-" + id.ToString("N"), $"qa-{id:N}@example.invalid", tenant.Id);
			user.AddRole(role.Id);
			await services.GetRequiredService<IIdentityUserRepository>().InsertAsync(user);
			linkedUser = id;
		}
		if (employee)
		{
			var record = (Employee)Activator.CreateInstance(typeof(Employee), nonPublic: true)!;
			Set(record, nameof(Employee.Id), Guid.NewGuid());
			Set(record, nameof(Employee.TenantId), tenant.Id);
			Set(record, nameof(Employee.RoleId), role.Id);
			Set(record, nameof(Employee.UserId), linkedUser);
			Set(record, nameof(Employee.EmployeeCode), "ROLE-QA-" + Guid.NewGuid().ToString("N"));
			Set(record, nameof(Employee.IsDeleted), archived);
			await services.GetRequiredService<IRepository<Employee, Guid>>().InsertAsync(record);
		}
		await unit.CompleteAsync();
		return (tenant.Id, role.Id);
	}

	private static void Set(Employee employee, string property, object? value) =>
		typeof(Employee).GetProperty(property)!.SetValue(employee, value);
}

[Trait("Module", "Security"), Trait("Tier", "Critical")]
public class RoleDeletionApi_Tests(RoleDeletionTestFactory factory) : IClassFixture<RoleDeletionTestFactory>
{
	private const string Url = "/api/identity/roles/";

	[Fact]
	public async Task Assigned_role_should_return_409_with_exact_user_count_and_preserve_memberships()
	{
		var data = await factory.SeedAsync(users: 2, employee: true);
		using var client = Client(data.TenantId);
		var response = await client.DeleteAsync(Url + data.RoleId);
		response.StatusCode.ShouldBe(HttpStatusCode.Conflict, await response.Content.ReadAsStringAsync());
		using var problem = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
		problem.RootElement.GetProperty("errorCode").GetString().ShouldBe(RoleErrorCodes.AssignedToUsers);
		problem.RootElement.GetProperty("data").GetProperty("UserCount").GetInt32().ShouldBe(2);
		problem.RootElement.GetProperty("detail").GetString().ShouldBe(
			"This role is assigned to 2 user(s) and cannot be deleted. Reassign or remove those users first.");
		(await client.GetAsync(Url + data.RoleId)).StatusCode.ShouldBe(HttpStatusCode.OK);
		using var scope = factory.Services.CreateScope();
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(data.TenantId);
		using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		(await scope.ServiceProvider.GetRequiredService<IIdentityUserRepository>().GetUserIdListByRoleIdAsync(data.RoleId)).Count.ShouldBe(2);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Employee_without_login_should_block_deletion_including_archived_records(bool archived)
	{
		var data = await factory.SeedAsync(employee: true, archived: archived);
		using var client = Client(data.TenantId);
		var response = await client.DeleteAsync(Url + data.RoleId);
		response.StatusCode.ShouldBe(HttpStatusCode.Conflict, await response.Content.ReadAsStringAsync());
		var body = await response.Content.ReadAsStringAsync();
		body.ShouldContain(RoleErrorCodes.ReferencedByEmployees);
		body.ShouldContain("1 employee record(s)");
		(await client.GetAsync(Url + data.RoleId)).StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	[Fact]
	public async Task Unassigned_role_should_delete_successfully()
	{
		var data = await factory.SeedAsync();
		using var client = Client(data.TenantId);
		var response = await client.DeleteAsync(Url + data.RoleId);
		response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
		(await client.GetAsync(Url + data.RoleId)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task Delete_should_require_permission_before_disclosing_usage()
	{
		var data = await factory.SeedAsync(users: 1);
		using var client = Client(data.TenantId);
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, IdentityPermissions.Roles.Default);
		var response = await client.DeleteAsync(Url + data.RoleId);
		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await response.Content.ReadAsStringAsync()).ShouldNotContain("UserCount");
	}

	[Fact]
	public async Task Other_tenant_should_not_delete_or_discover_assigned_role()
	{
		var data = await factory.SeedAsync(users: 1);
		var other = await factory.SeedAsync();
		using var client = Client(other.TenantId);
		var response = await client.DeleteAsync(Url + data.RoleId);
		response.StatusCode.ShouldBe(HttpStatusCode.NotFound, await response.Content.ReadAsStringAsync());
		(await response.Content.ReadAsStringAsync()).ShouldNotContain("UserCount");
	}

	[Fact]
	public async Task Direct_application_service_should_enforce_the_dependency_rule()
	{
		var data = await factory.SeedAsync(users: 1);
		using var scope = factory.Services.CreateScope();
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(data.TenantId);
		using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		var service = scope.ServiceProvider.GetRequiredService<IIdentityRoleAppService>();
		var exception = await Should.ThrowAsync<BusinessException>(() => service.DeleteAsync(data.RoleId));
		exception.Code.ShouldBe(RoleErrorCodes.AssignedToUsers);
	}

	private HttpClient Client(Guid tenantId)
	{
		var client = factory.CreateClient();
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, tenantId.ToString());
		return client;
	}
}
