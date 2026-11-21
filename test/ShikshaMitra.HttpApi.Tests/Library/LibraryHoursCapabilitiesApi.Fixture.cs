using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Library.Setting.GeneralSetting.LibraryTimings;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.Library;

public partial class LibraryHoursCapabilitiesApi_Tests
{
	private HttpClient HoursClient(Guid actor, bool view, bool add)
	{
		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, TestAuthHandler.TestTenantId);
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, actor.ToString("D"));
		var grants = view ? ShikshaMitraSubmodulePermissions.Library.Settings_LibraryHours.View : string.Empty;
		if (add) grants += "," + ShikshaMitraSubmodulePermissions.Library.Settings_LibraryHours.Add;
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, grants.Trim(','));
		return client;
	}

	private async Task<(Guid Year, Guid Actor)> SeedHoursContextAsync(bool wide)
	{
		using var scope = Factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(tenantId);
		using var unit = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var actor = Guid.NewGuid();
		var name = "hours-" + actor.ToString("N");
		await services.GetRequiredService<IIdentityUserRepository>().InsertAsync(new(actor, name, name + "@example.test", tenantId), true);
		var academicYear = await services.GetRequiredService<AcademicYearManager>().CreateAsync(name,
			new DateTime(2026, 4, 1), new DateTime(2027, 3, 31));
		await services.GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(academicYear, true);
		var year = academicYear.Id;
		await services.GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(
			new(Guid.NewGuid(), tenantId, userId: actor, academicYearId: wide ? null : year), true);
		await services.GetRequiredService<IRepository<LibraryTiming, Guid>>().InsertAsync(
			new(Guid.NewGuid(), "Monday", "09:00:00", "17:00:00", 0, year, tenantId), true);
		await unit.CompleteAsync();
		await services.GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(actor);
		return (year, actor);
	}
}
