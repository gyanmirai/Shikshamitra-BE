using System;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Data;
using ShikshaMitra.Seeders;
using ShikshaMitra.TenantSettings;
using Volo.Abp.Auditing;
using Volo.Abp.Authorization;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.FeatureManagement;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Volo.Abp.SettingManagement;

namespace ShikshaMitra;

/// <summary>
/// Custom <see cref="WebApplicationFactory{TEntryPoint}"/> that boots the
/// ShikshaMitra host with in-memory SQLite and a test auth handler.
/// Shared across all in-process API tests via xUnit collection fixture.
/// </summary>
public class ShikshaMitraApiTestFactory : WebApplicationFactory<Program>
{
	private SqliteConnection? _keepAliveConnection;

	public Guid SeededRoleId { get; private set; }
	public Guid SeededShiftId { get; private set; }

	protected override IHost CreateHost(IHostBuilder builder)
	{
		var host = base.CreateHost(builder);

		Task.Run(async () =>
		{
			using var scope = host.Services.CreateScope();
			var dataSeeder = scope.ServiceProvider.GetRequiredService<IDataSeeder>();
			await dataSeeder.SeedAsync(new DataSeedContext());

			await ResolveSeededLookupsAsync(scope.ServiceProvider);
		}).GetAwaiter().GetResult();

		return host;
	}

	private async Task ResolveSeededLookupsAsync(IServiceProvider services)
	{
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		var currentTenant = services.GetRequiredService<ICurrentTenant>();

		using (currentTenant.Change(tenantId))
		{
			var roleRepo = services.GetRequiredService<IIdentityRoleRepository>();
			var roles = await roleRepo.GetListAsync();
			var teacherRole = roles.FirstOrDefault(r => r.Name == "Teacher");
			SeededRoleId = teacherRole?.Id ?? Guid.Empty;

			var shiftRepo = services.GetRequiredService<IRepository<ShiftManagement, Guid>>();
			var shifts = await shiftRepo.GetListAsync();
			var fullDayShift = shifts.FirstOrDefault(s => s.ShiftName == "Full Day Shift");
			SeededShiftId = fullDayShift?.Id ?? Guid.Empty;
		}
	}

	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		builder.UseEnvironment("Development");

		builder.ConfigureServices(services =>
		{
			ReplaceDbWithSqlite(services);
			DisableBackgroundJobs(services);
			DisableDynamicStores(services);
			DisableAuditing(services);
		});

		builder.ConfigureTestServices(services =>
		{
			AddTestAuthentication(services);
			services.AddTransient<StudentSeederContributor, NoOpStudentSeeder>();
		});
	}

	private void ReplaceDbWithSqlite(IServiceCollection services)
	{
		_keepAliveConnection = new SqliteConnection("Data Source=:memory:");
		_keepAliveConnection.Open();

		services.Configure<AbpDbContextOptions>(options =>
		{
			options.Configure(ctx =>
			{
				ctx.DbContextOptions.UseSqlite(_keepAliveConnection);
			});
		});

		var dbOptions = new DbContextOptionsBuilder<ShikshaMitraDbContext>()
			.UseSqlite(_keepAliveConnection)
			.Options;

		using var context = new ShikshaMitraDbContext(dbOptions);
		context.GetService<IRelationalDatabaseCreator>().CreateTables();
	}

	private static void DisableBackgroundJobs(IServiceCollection services)
	{
		services.Configure<AbpBackgroundJobOptions>(o => o.IsJobExecutionEnabled = false);
	}

	private static void DisableAuditing(IServiceCollection services)
	{
		services.Configure<AbpAuditingOptions>(o => o.IsEnabled = false);
	}

	private static void DisableDynamicStores(IServiceCollection services)
	{
		services.Configure<FeatureManagementOptions>(o =>
		{
			o.SaveStaticFeaturesToDatabase = false;
			o.IsDynamicFeatureStoreEnabled = false;
		});
		services.Configure<PermissionManagementOptions>(o =>
		{
			o.SaveStaticPermissionsToDatabase = false;
			o.IsDynamicPermissionStoreEnabled = false;
		});
		services.Configure<SettingManagementOptions>(o =>
		{
			o.SaveStaticSettingsToDatabase = false;
			o.IsDynamicSettingStoreEnabled = false;
		});
	}

	private static void AddTestAuthentication(IServiceCollection services)
	{
		services.AddAuthentication(TestAuthHandler.SchemeName)
			.AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
				TestAuthHandler.SchemeName, _ => { });

		services.PostConfigure<AuthenticationOptions>(options =>
		{
			options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
			options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
			options.DefaultScheme = TestAuthHandler.SchemeName;
			options.DefaultForbidScheme = TestAuthHandler.SchemeName;

			foreach (var scheme in options.SchemeMap.Values)
			{
				if (scheme.Name != TestAuthHandler.SchemeName)
				{
					scheme.HandlerType = typeof(TestAuthHandler);
				}
			}
		});

		services.AddSingleton<IAuthorizationHandler, AllowAllAuthorizationHandler>();

		services.AddAlwaysAllowAuthorization();

		services.AddTransient<Volo.Abp.Authorization.Permissions.IPermissionChecker,
			AlwaysGrantPermissionChecker>();
	}

	protected override void Dispose(bool disposing)
	{
		base.Dispose(disposing);
		if (disposing)
		{
			_keepAliveConnection?.Dispose();
		}
	}
}
