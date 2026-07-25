using System;
using System.Data.Common;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Infrastructure;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Data;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Seeders;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using ShikshaMitra.TenantSettings;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.AcademicSetups.Divisions;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using Volo.Abp.AspNetCore.Mvc.Libs;
using Volo.Abp.Auditing;
using Volo.Abp.Security.Claims;
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
using Volo.Abp.Uow;

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
	public Guid SeededAcademicYearId { get; private set; }
	public Guid SeededBoardId { get; private set; }
	public Guid SeededMediumStageId { get; private set; }
	public Guid SeededMediumId { get; private set; }
	public Guid SeededStandardId { get; private set; }
	public Guid SeededDivisionId { get; private set; }
	public Guid SeededFreshRouteDefinitionId { get; private set; }

	protected override IHost CreateHost(IHostBuilder builder)
	{
		var host = base.CreateHost(builder);

		Task.Run(async () =>
		{
			// Seeding completes UoWs that dispose the scoped DbContext; resolve lookups in a new scope.
			using (var seedScope = host.Services.CreateScope())
			{
				var dataSeeder = seedScope.ServiceProvider.GetRequiredService<IDataSeeder>();
				await dataSeeder.SeedAsync(new DataSeedContext()).ConfigureAwait(false);
			}

			using var lookupScope = host.Services.CreateScope();
			await ResolveSeededLookupsAsync(lookupScope.ServiceProvider).ConfigureAwait(false);
		}).GetAwaiter().GetResult();

		return host;
	}

	private async Task ResolveSeededLookupsAsync(IServiceProvider services)
	{
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		var currentTenant = services.GetRequiredService<ICurrentTenant>();
		var uowManager = services.GetRequiredService<IUnitOfWorkManager>();

		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		using (currentTenant.Change(tenantId))
		{
			// Avoid GetListAsync on IIdentityRoleRepository: implementation can materialize heavy graphs
			// and stall SQLite in-memory for many minutes.
			var roleRepo = services.GetRequiredService<IIdentityRoleRepository>();
			var lookupNormalizer = services.GetRequiredService<ILookupNormalizer>();
			var teacherRole = await roleRepo.FindByNormalizedNameAsync(lookupNormalizer.NormalizeName("Teacher"));
			SeededRoleId = teacherRole?.Id ?? Guid.Empty;

			// Ensure the test auth user (TestAuthHandler.TestUserId) exists in IdentityUsers so that
			// any app service query that JOINs with IdentityUser on requestorId/approvedBy resolves
			// correctly. Without this, the charge requestor JOIN returns null and the charge is
			// reported as "not found" immediately after creation.
			await EnsureTestAuthUserExistsAsync(services, tenantId, lookupNormalizer);
			await EnsureTestAuthEmployeeExistsAsync(services);
			await EnsureTestAuthUserHasTenantWideScopeAsync(services, tenantId);

			var shiftRepo = services.GetRequiredService<IRepository<ShiftManagement, Guid>>();
			var fullDayShift = await (await shiftRepo.GetQueryableAsync()).AsNoTracking()
				.FirstOrDefaultAsync(s => s.ShiftName == "Full Day Shift");
			SeededShiftId = fullDayShift?.Id ?? Guid.Empty;

			var academicYearRepo = services.GetRequiredService<IAcademicYearRepository>();
			var firstYear = await (await academicYearRepo.GetQueryableAsync()).AsNoTracking()
				.OrderBy(y => y.Id)
				.FirstOrDefaultAsync();
			SeededAcademicYearId = firstYear?.Id ?? Guid.Empty;
			SeededFreshRouteDefinitionId = await EnsureTestFreshAdmissionRouteAsync(
				services,
				tenantId,
				SeededAcademicYearId);

			var mediumStageRepo = services.GetRequiredService<IEducationMediumStageRepository>();
			var firstStage = await (await mediumStageRepo.GetQueryableAsync()).AsNoTracking()
				.OrderBy(s => s.Id)
				.FirstOrDefaultAsync();
			SeededMediumStageId = firstStage?.Id ?? Guid.Empty;

			var standardRepo = services.GetRequiredService<IStandardRepository>();
			var firstStandard = await (await standardRepo.GetQueryableAsync()).AsNoTracking()
				.OrderBy(s => s.Id)
				.FirstOrDefaultAsync();
			SeededStandardId = firstStandard?.Id ?? Guid.Empty;

			// Resolve the EducationMedium.Id from the standard's EducationMediumStage.
			// AcademicMediumId in enrollment DTOs refers to EducationMedium.Id, not EducationMediumStage.Id.
			if (firstStandard != null)
			{
				SeededMediumStageId = firstStandard.EducationMediumStageId;
				var standardStage = await (await mediumStageRepo.GetQueryableAsync()).AsNoTracking()
					.FirstOrDefaultAsync(s => s.Id == firstStandard.EducationMediumStageId);
				SeededMediumId = standardStage?.EducationMediumId ?? Guid.Empty;
			}
			else if (firstStage != null)
			{
				SeededMediumId = firstStage.EducationMediumId;
			}

			if (SeededMediumId != Guid.Empty)
			{
				var mediumRepo = services.GetRequiredService<IEducationMediumRepository>();
				var medium = await (await mediumRepo.GetQueryableAsync()).AsNoTracking()
					.FirstOrDefaultAsync(m => m.Id == SeededMediumId);
				SeededBoardId = medium?.EducationBoardId ?? Guid.Empty;
			}

			var divisionRepo = services.GetRequiredService<IDivisionRepository>();
			var firstDivision = await (await divisionRepo.GetQueryableAsync()).AsNoTracking()
				.OrderBy(d => d.Id)
				.FirstOrDefaultAsync();
			SeededDivisionId = firstDivision?.Id ?? Guid.Empty;

			await uow.CompleteAsync();
		}
	}

	private static async Task<Guid> EnsureTestFreshAdmissionRouteAsync(
		IServiceProvider services,
		Guid tenantId,
		Guid academicYearId)
	{
		if (academicYearId == Guid.Empty)
		{
			return Guid.Empty;
		}

		var repository = services.GetRequiredService<IRepository<AdmissionCrmRouteDefinition, Guid>>();
		var now = DateTime.UtcNow;
		var queryable = await repository.GetQueryableAsync();
		var existing = await queryable.FirstOrDefaultAsync(definition =>
			definition.AcademicYearId == academicYearId &&
			definition.RouteType == AdmissionCrmRouteType.Fresh &&
			definition.Status == AdmissionCrmRouteDefinitionStatus.Published &&
			definition.EffectiveFrom <= now &&
			(!definition.EffectiveUntil.HasValue || now < definition.EffectiveUntil.Value));
		if (existing != null)
		{
			return existing.Id;
		}

		var definition = new AdmissionCrmRouteDefinition(
			Guid.NewGuid(),
			tenantId,
			academicYearId,
			"TEST-FRESH",
			"Fresh admission",
			AdmissionCrmRouteType.Fresh,
			1,
			"{\"requiresPolicyEvaluation\":false,\"requiresDistinctApproval\":false,\"requiredEvidenceCodes\":[]}",
			now.AddDays(-1),
			now.AddYears(2));
		definition.Submit(Guid.Parse(TestAuthHandler.TestUserId));
		definition.Publish(Guid.NewGuid());
		await repository.InsertAsync(definition);
		return definition.Id;
	}

	/// <summary>
	/// Inserts an <see cref="IdentityUser"/> row whose Id matches <see cref="TestAuthHandler.TestUserId"/>
	/// if it doesn't already exist. Required because app-service queries (e.g. charge detail, salary slip)
	/// do an INNER JOIN on <c>IdentityUsers</c> for the requestor/approver. Without this row the JOIN
	/// produces no result and the entity appears to not exist immediately after creation.
	/// </summary>
	private static async Task EnsureTestAuthUserExistsAsync(
		IServiceProvider services,
		Guid tenantId,
		ILookupNormalizer lookupNormalizer)
	{
		var userRepo = services.GetRequiredService<IIdentityUserRepository>();
		var testUserId = Guid.Parse(TestAuthHandler.TestUserId);

		var existing = await userRepo.FindAsync(testUserId);
		if (existing != null)
			return;

		var dbContext = services.GetRequiredService<ShikshaMitraDbContext>();
		var testUser = new Volo.Abp.Identity.IdentityUser(testUserId, TestAuthHandler.TestUserName, TestAuthHandler.TestEmail, tenantId);
		await dbContext.Set<Volo.Abp.Identity.IdentityUser>().AddAsync(testUser);
		await dbContext.SaveChangesAsync();
	}

	private async Task EnsureTestAuthEmployeeExistsAsync(IServiceProvider services)
	{
		var userId = Guid.Parse(TestAuthHandler.TestUserId);
		var employeeRepository = services.GetRequiredService<IEmployeeRepository>();
		if (await employeeRepository.GetEmployeeByUserIdAsync(userId) != null)
		{
			return;
		}

		var employeeManager = services.GetRequiredService<EmployeeManager>();
		var employee = await employeeManager.CreateEmployeeAsync(
			userId,
			SeededRoleId,
			"API",
			"Test User",
			null,
			"API Test",
			EmployeeStatusType.Active,
			new DateOnly(1990, 1, 1),
			mobileNo: "9898989898",
			email: TestAuthHandler.TestEmail,
			panNo: "APITS1234A",
			adharNo: "900000000001",
			department: "Quality Assurance",
			designation: "API Test Operator",
			dateOfJoining: new DateOnly(2020, 1, 1),
			employeeGroup: EmployeeGroupType.NonTeaching);
		await employeeRepository.InsertAsync(employee, autoSave: true);
	}

	private static async Task EnsureTestAuthUserHasTenantWideScopeAsync(
		IServiceProvider services,
		Guid tenantId)
	{
		var scopeRepository = services.GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
		var userScopeService = services.GetRequiredService<IUserScopeService>();
		var testUserId = Guid.Parse(TestAuthHandler.TestUserId);
		var queryable = await scopeRepository.GetQueryableAsync();

		if (!queryable.Any(x =>
			    x.UserId == testUserId &&
			    x.AcademicYearId == null &&
			    x.EducationBoardId == null &&
			    x.EducationMediumId == null &&
			    x.EducationMediumStageId == null &&
			    x.StandardId == null &&
			    x.DivisionId == null &&
			    x.IsActive))
		{
			await scopeRepository.InsertAsync(
				new UserScopeAssignment(
					Guid.NewGuid(),
					tenantId,
					userId: testUserId,
					description: "API test default tenant-wide scope"),
				autoSave: true);
		}

		await userScopeService.InvalidateUserScopeCacheAsync(testUserId);
	}

	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		builder.UseEnvironment("Development");
		builder.ConfigureAppConfiguration(configuration =>
		{
			configuration.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["DevAutoAuth:Enabled"] = "false",
				["ShikshaMitra:Seeders:HR:SeedSampleApprovedLeaves"] = "false"
			});
		});

		builder.ConfigureServices(services =>
		{
			ReplaceDbWithSqlite(services);
			DisableBackgroundJobs(services);
			DisableDynamicStores(services);
			DisableAuditing(services);
			DisableDynamicClaims(services);
		});

		builder.ConfigureTestServices(services =>
		{
			AddTestAuthentication(services);
			RemoveAbpIdentityDataSeedContributor(services);
			services.AddTransient<StudentSeederContributor, NoOpStudentSeeder>();
			// Replace the PostgreSQL-specific receipt number repo with an in-memory fake
			// so SQLite doesn't receive the INSERT … ON CONFLICT … RETURNING syntax.
			services.AddTransient<IReceiptNumberSequenceRepository, FakeReceiptNumberSequenceRepository>();
			// ABP's bundling module enables the wwwroot/libs check which blocks all
			// API requests when npm packages are not installed. PostConfigure runs
			// after all Configure<> calls to ensure this override wins.
			services.PostConfigure<AbpMvcLibsOptions>(opts => opts.CheckLibs = false);
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

	private static void DisableDynamicClaims(IServiceCollection services)
	{
		services.Configure<AbpClaimsPrincipalFactoryOptions>(o => o.IsDynamicClaimsEnabled = false);
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

		services.AddTransient<Volo.Abp.Authorization.Permissions.IPermissionChecker,
			AlwaysGrantPermissionChecker>();
	}

	private static void RemoveAbpIdentityDataSeedContributor(IServiceCollection services)
	{
		var descriptors = services
			.Where(descriptor =>
				descriptor.ServiceType == typeof(IDataSeedContributor) &&
				descriptor.ImplementationType?.FullName == "Volo.Abp.Identity.IdentityDataSeedContributor")
			.ToList();

		foreach (var descriptor in descriptors)
		{
			services.Remove(descriptor);
		}
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
