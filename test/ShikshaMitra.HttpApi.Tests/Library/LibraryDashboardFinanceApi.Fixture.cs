using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.Library;

public partial class LibraryDashboardFinanceApi_Tests
{
	private HttpClient DashboardClient(Guid actor, string action)
	{
		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, TestAuthHandler.TestTenantId);
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, actor.ToString("D"));
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestRolesHeader, "dashboard-reader");
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, action);
		return client;
	}

	private async Task<(Guid Year, Guid Actor)> SeedDashboardContextAsync(bool assignScope)
	{
		using var scope = Factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(tenantId);
		using var unit = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var actor = Guid.NewGuid();
		var name = "dashboard-" + actor.ToString("N");
		await services.GetRequiredService<IIdentityUserRepository>().InsertAsync(new(actor, name, name + "@example.test", tenantId), true);
		var year = await services.GetRequiredService<AcademicYearManager>().CreateAsync(name,
			new DateTime(2026, 4, 1), new DateTime(2027, 3, 31));
		await services.GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(year, true);
		if (assignScope)
			await services.GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(
				new(Guid.NewGuid(), tenantId, userId: actor), true);
		await unit.CompleteAsync();
		await services.GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(actor);
		return (year.Id, actor);
	}
}
