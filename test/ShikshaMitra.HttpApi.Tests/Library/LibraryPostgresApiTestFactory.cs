using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using ShikshaMitra.EntityFrameworkCore.Finance.Fees.Receipts;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Finance.Fees.FeeCollection;
using Volo.Abp.DependencyInjection;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Seeders;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace ShikshaMitra.Library;

internal sealed class LibraryPostgresApiTestFactory : ShikshaMitraApiTestFactory
{
	private readonly string connectionString;
	private readonly LibraryMigrationPostgresFixture database;
	private readonly IClock? collectionClock;
	public LibraryPostgresTransactionObserver Transactions { get; } = new();
	public Guid YearId { get; private set; }
	public Guid BoardId { get; private set; }
	public Guid MediumId { get; private set; }
	public Guid StageId { get; private set; }
	public Guid StandardId { get; private set; }
	protected override bool InitializeTestData => false;

	public LibraryPostgresApiTestFactory(LibraryMigrationPostgresFixture database, IClock? collectionClock = null)
	{
		this.database = database;
		this.collectionClock = collectionClock;
		using var context = database.CreateContext();
		connectionString = context.Database.GetConnectionString()
			?? throw new InvalidOperationException("The owned migrated database has no connection.");
	}

	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		base.ConfigureWebHost(builder);
		builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(new Dictionary<string, string?>
		{
			["ConnectionStrings:Default"] = connectionString,
			[ShikshaMitraSeederProfile.ContextPropertyName] = ShikshaMitraSeederProfile.Minimal,
			["Logging:LogLevel:Default"] = "Warning"
		}));
		builder.ConfigureTestServices(services =>
		{
			services.PostConfigure<AbpClockOptions>(options => options.Kind = DateTimeKind.Utc);
			services.RemoveAll<IReceiptNumberSequenceRepository>();
			services.AddTransient<IReceiptNumberSequenceRepository, EfCoreReceiptNumberSequenceRepository>();
			if (collectionClock is { } explicitCollectionClock)
			{
				// Keep host/audit/receipt clocks UTC. Only the real collection
				// manager receives this alternate real ABP clock.
				services.RemoveAll<FeeCollectionManager>();
				services.AddTransient<FeeCollectionManager>(provider =>
				{
					var manager = ActivatorUtilities.CreateInstance<FeeCollectionManager>(provider, explicitCollectionClock);
					manager.LazyServiceProvider = new AbpLazyServiceProvider(provider);
					return manager;
				});
			}
		});
	}

	protected override void ConfigureTestDatabase(IServiceCollection services)
	{
		// Use the migrated production schema for HTTP workflow tests.
		services.Configure<AbpDbContextOptions>(options => options.Configure(context => context.DbContextOptions
			.UseNpgsql(connectionString).AddInterceptors(Transactions)));
	}

	protected override IHost CreateHost(IHostBuilder builder)
	{
		Task.Run(database.MigrateToCurrentAsync).GetAwaiter().GetResult();
		var host = base.CreateHost(builder);
		Task.Run(() => SeedPrerequisitesAsync(host.Services)).GetAwaiter().GetResult();
		return host;
	}

	private async Task SeedPrerequisitesAsync(IServiceProvider hostServices)
	{
		using var scope = hostServices.CreateScope();
		var services = scope.ServiceProvider;
		var currentTenant = services.GetRequiredService<ICurrentTenant>();
		using var unit = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		using (currentTenant.Change(null))
			await services.GetRequiredService<TenantSeederContributor>().SeedAsync(new DataSeedContext());
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		using (currentTenant.Change(tenantId))
		{
			var year = await services.GetRequiredService<AcademicYearManager>().CreateAsync("Library workflow 2026-27",
				new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2027, 3, 31, 0, 0, 0, DateTimeKind.Utc));
			await services.GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(year, true);
			YearId = year.Id;
			var board = await services.GetRequiredService<EducationBoardManager>().CreateAsync(YearId, EducationBoardType.CBSE,
				year.StartDate, year.EndDate);
			await services.GetRequiredService<IRepository<EducationBoard, Guid>>().InsertAsync(board, true);
			BoardId = board.Id;
			var medium = await services.GetRequiredService<EducationMediumManager>().CreateAsync(BoardId, "English");
			await services.GetRequiredService<IRepository<EducationMedium, Guid>>().InsertAsync(medium, true);
			MediumId = medium.Id;
			var stage = await services.GetRequiredService<EducationMediumStageManager>().CreateAsync(MediumId, "Middle school");
			await services.GetRequiredService<IRepository<EducationMediumStage, Guid>>().InsertAsync(stage, true);
			StageId = stage.Id;
			var standard = await services.GetRequiredService<StandardManager>().CreateAsync(StageId, "5", 10, null);
			await services.GetRequiredService<IRepository<Standard, Guid>>().InsertAsync(standard, true);
			StandardId = standard.Id;
			var userId = Guid.Parse(TestAuthHandler.TestUserId);
			await services.GetRequiredService<IIdentityUserRepository>().InsertAsync(
				new IdentityUser(userId, TestAuthHandler.TestUserName, TestAuthHandler.TestEmail, tenantId), true);
			await services.GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(new UserScopeAssignment(
				Guid.NewGuid(), tenantId, userId: userId, description: "PostgreSQL Library workflow tenant-wide operator"), true);
			await services.GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(userId);
		}
		await unit.CompleteAsync();
	}
}
