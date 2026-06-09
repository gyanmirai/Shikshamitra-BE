using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Audit;
using ShikshaMitra.Finance.FeeRefund;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Finance.Fees.Settings.ConcessionTypes;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.OpenIddict;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra;

public class ShikshaMitraSeederContributor : IDataSeedContributor, ITransientDependency
{
	private readonly IServiceProvider _serviceProvider;
	private readonly IConfiguration _configuration;
	private readonly ILogger<ShikshaMitraSeederContributor> _logger;

	public ShikshaMitraSeederContributor(
		IServiceProvider serviceProvider,
		IConfiguration configuration,
		ILogger<ShikshaMitraSeederContributor> logger)
	{
		_serviceProvider = serviceProvider;
		_configuration = configuration;
		_logger = logger;
	}

	public async Task SeedAsync(DataSeedContext context)
	{
		var options = Seeders.ShikshaMitraSeederOptions.FromConfiguration(_configuration);
		context.WithProperty(Seeders.ShikshaMitraSeederProfile.ContextPropertyName, options.Profile);

		if (!options.Enabled)
		{
			_logger.LogInformation(
				"[SeederSummary] Seeders are disabled by configuration. Profile={Profile}",
				options.Profile);
			return;
		}

		using (var scope = _serviceProvider.CreateScope())
		{
			var summary = new SeederRunSummary();
			foreach (var step in BuildHostSteps(scope.ServiceProvider).OrderBy(x => x.Order))
			{
				await RunSeederStepAsync(scope.ServiceProvider, step, context, tenantId: null, options, summary);
			}

			// Retrieve tenant IDs from the context.
			if (!context.Properties.TryGetValue("TenantIds", out var tenantIdsObj) || tenantIdsObj == null)
			{
				return;
			}
			var tenantIds = tenantIdsObj as List<Guid>;
			if (tenantIds == null || tenantIds.Count == 0)
			{
				return;
			}

			// Resolve ICurrentTenant to change the tenant context.
			var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();

			// Iterate through each tenant ID and seed remaining data.
			foreach (var tenantId in tenantIds)
			{
				using (currentTenant.Change(tenantId))
				{
					// Now that the tenant is set, run the remaining seeders.
					foreach (var step in BuildTenantSteps(scope.ServiceProvider).OrderBy(x => x.Order))
					{
						await RunSeederStepAsync(scope.ServiceProvider, step, context, tenantId, options, summary);
					}
				}
			}

			_logger.LogInformation(
				"[SeederSummary] Completed seeding. Profile={Profile}, Ran={RanCount}, SkippedByMarker={MarkerSkippedCount}, SkippedByGuard={GuardSkippedCount}, SkippedByProfile={ProfileSkippedCount}, ElapsedMs={ElapsedMs}",
				options.Profile,
				summary.RanCount,
				summary.MarkerSkippedCount,
				summary.GuardSkippedCount,
				summary.ProfileSkippedCount,
				summary.ElapsedMilliseconds);
		}
	}

	private static IReadOnlyList<Seeders.IShikshaMitraSeederStep> BuildHostSteps(IServiceProvider serviceProvider) =>
	[
		new Seeders.DelegateShikshaMitraSeederStep(
			"OpenIddict",
			"OpenIddict:v1",
			10,
			Seeders.ShikshaMitraSeederTenantMode.Host,
			async (context, _, _) =>
			{
				await serviceProvider.GetRequiredService<OpenIddictDataSeedContributor>().SeedAsync(context);
				var unitOfWorkManager = serviceProvider.GetRequiredService<IUnitOfWorkManager>();
				if (unitOfWorkManager.Current != null)
				{
					await unitOfWorkManager.Current.SaveChangesAsync();
				}
			},
			useCompletionMarker: false),
		new Seeders.DelegateShikshaMitraSeederStep(
			"TenantSeeder",
			"TenantSeeder:v1",
			20,
			Seeders.ShikshaMitraSeederTenantMode.Host,
			async (context, _, _) => await serviceProvider.GetRequiredService<Seeders.TenantSeederContributor>().SeedAsync(context),
			useCompletionMarker: false)
	];

	private static IReadOnlyList<Seeders.IShikshaMitraSeederStep> BuildTenantSteps(IServiceProvider serviceProvider) =>
	[
		TenantStep<Seeders.EventTypeSeederContributor>(serviceProvider, "EventTypeSeeder", "EventTypeSeeder:v1", 100, Seeders.ShikshaMitraSeederProfile.All),
		TenantStep<Seeders.RolesSeederContributor>(serviceProvider, "RolesSeeder", "RolesSeeder:v2", 110, Seeders.ShikshaMitraSeederProfile.All),
		TenantStep<Seeders.RolePermissionSeederContributor>(serviceProvider, "RolePermissionSeeder", "RolePermissionSeeder:v5", 120, Seeders.ShikshaMitraSeederProfile.All),
		TenantStep<Seeders.FinanceFeeAdjustmentPermissionMigrationSeederContributor>(
			serviceProvider,
			"FinanceFeeAdjustmentPermissionMigration",
			"FinanceFeeAdjustmentPermissionMigration:v1",
			121,
			Seeders.ShikshaMitraSeederProfile.All),
		TenantStep<Seeders.UserSeederContributor>(serviceProvider, "UserSeeder", "UserSeeder:v2", 130, Seeders.ShikshaMitraSeederProfile.All),
		TenantStep<Seeders.AcademicSetupSeederContributor>(serviceProvider, "AcademicSetupSeeder", "AcademicSetupSeeder:v2", 140, Seeders.ShikshaMitraSeederProfile.All),
		TenantStep<Seeders.DemoAcademicAndInstituteSeederContributor>(serviceProvider, "DemoAcademicAndInstituteSeeder", "DemoAcademicAndInstituteSeeder:v2", 150, Seeders.ShikshaMitraSeederProfile.Demo),
		TenantStep<Seeders.HRSeederContributor>(serviceProvider, "HRSeeder", "HRSeeder:v4", 160, Seeders.ShikshaMitraSeederProfile.Demo),
		TenantStep<Seeders.StudentSeederContributor>(serviceProvider, "StudentSeeder", "StudentSeeder:v1", 170, Seeders.ShikshaMitraSeederProfile.Demo, ExistingEntityGuard<Student>(serviceProvider)),
		TenantStep<Seeders.FeeRefundSeederContributor>(serviceProvider, "FeeRefundSeeder", "FeeRefundSeeder:v1", 180, Seeders.ShikshaMitraSeederProfile.Demo, ExistingEntityGuard<FeeRefundRequest>(serviceProvider)),
		TenantStep<Seeders.FeeAssignmentRepairSeederContributor>(serviceProvider, "FeeAssignmentRepairSeeder", "FeeAssignmentRepairSeeder:v2", 185, Seeders.ShikshaMitraSeederProfile.Demo),
		TenantStep<Seeders.FeeCollectionSeederContributor>(serviceProvider, "FeeCollectionSeeder", "FeeCollectionSeeder:v2", 190, Seeders.ShikshaMitraSeederProfile.Demo, ExistingEntityGuard<FeeRecordMetadata>(serviceProvider)),
		TenantStep<Seeders.FeeReceiptSeederContributor>(serviceProvider, "FeeReceiptSeeder", "FeeReceiptSeeder:v1", 200, Seeders.ShikshaMitraSeederProfile.Demo, ExistingEntityGuard<FeePaymentReceipt>(serviceProvider)),
		TenantStep<Seeders.FinanceAuditSeederContributor>(serviceProvider, "FinanceAuditSeeder", "FinanceAuditSeeder:v1", 205, Seeders.ShikshaMitraSeederProfile.Demo),
		TenantStep<Seeders.ReconciliationSeederContributor>(serviceProvider, "ReconciliationSeeder", "ReconciliationSeeder:v1", 210, Seeders.ShikshaMitraSeederProfile.Demo),
		TenantStep<Seeders.ConcessionTypeSeederContributor>(serviceProvider, "ConcessionTypeSeeder", "ConcessionTypeSeeder:v1", 220, Seeders.ShikshaMitraSeederProfile.Demo, ExistingEntityGuard<ConcessionType>(serviceProvider)),
		TenantStep<Seeders.TestLoginSeederContributor>(serviceProvider, "TestLoginSeeder", "TestLoginSeeder:v2", 230, Seeders.ShikshaMitraSeederProfile.Demo),
		TenantStep<Seeders.StaffUserScopeSeederContributor>(serviceProvider, "StaffUserScopeSeeder", "StaffUserScopeSeeder:v2", 240, Seeders.ShikshaMitraSeederProfile.Demo)
	];

	private static Seeders.IShikshaMitraSeederStep TenantStep<TSeeder>(
		IServiceProvider serviceProvider,
		string name,
		string version,
		int order,
		IReadOnlySet<string> profiles,
		Func<DataSeedContext, Guid?, string, Task<bool>>? canSkipAsync = null)
		where TSeeder : class
	{
		return new Seeders.DelegateShikshaMitraSeederStep(
			name,
			version,
			order,
			Seeders.ShikshaMitraSeederTenantMode.Tenant,
			async (context, _, _) =>
			{
				var seeder = serviceProvider.GetRequiredService<TSeeder>();
				switch (seeder)
				{
					case IDataSeedContributor dataSeedContributor:
						await dataSeedContributor.SeedAsync(context);
						break;
					case Seeders.BaseSeederContributor baseSeederContributor:
						await baseSeederContributor.SeedAsync(context);
						break;
					default:
						throw new InvalidOperationException($"Seeder '{typeof(TSeeder).Name}' does not expose a supported SeedAsync method.");
				}
			},
			canSkipAsync: canSkipAsync,
			profiles: profiles);
	}

	private static Func<DataSeedContext, Guid?, string, Task<bool>> ExistingEntityGuard<TEntity>(IServiceProvider serviceProvider)
		where TEntity : class, IEntity<Guid>
	{
		return async (_, _, _) => await serviceProvider.GetRequiredService<IRepository<TEntity, Guid>>().AnyAsync();
	}

	private async Task RunSeederStepAsync(
		IServiceProvider serviceProvider,
		Seeders.IShikshaMitraSeederStep step,
		DataSeedContext context,
		Guid? tenantId,
		Seeders.ShikshaMitraSeederOptions options,
		SeederRunSummary summary)
	{
		if (!step.RunsInProfile(options.Profile))
		{
			summary.ProfileSkippedCount++;
			_logger.LogInformation(
				"[SeederTiming] {SeederName} skipped for TenantId={TenantId}; profile {Profile} does not run this step.",
				step.Name,
				tenantId,
				options.Profile);
			return;
		}

		var markerRepository = serviceProvider.GetRequiredService<IRepository<Seeders.SeedExecutionMarker, Guid>>();
		if (await ShouldSkipByMarkerAsync(markerRepository, step, tenantId, options))
		{
			summary.MarkerSkippedCount++;
			_logger.LogInformation(
				"[SeederTiming] {SeederName} skipped for TenantId={TenantId}; completion marker exists for Version={Version}, Profile={Profile}.",
				step.Name,
				tenantId,
				step.Version,
				options.Profile);
			return;
		}

		if (!options.ForceRerun && await step.CanSkipAsync(context, tenantId, options.Profile))
		{
			summary.GuardSkippedCount++;
			await MarkCompletedAsync(markerRepository, step, tenantId, options.Profile, 0);
			_logger.LogInformation(
				"[SeederTiming] {SeederName} skipped for TenantId={TenantId}; step guard returned true.",
				step.Name,
				tenantId);
			return;
		}

		var stopwatch = Stopwatch.StartNew();
		try
		{
			await MarkStartedAsync(markerRepository, step, tenantId, options.Profile);
			await step.SeedAsync(context, tenantId, options.Profile);
			stopwatch.Stop();
			await MarkCompletedAsync(markerRepository, step, tenantId, options.Profile, stopwatch.ElapsedMilliseconds);
			summary.RanCount++;
			summary.ElapsedMilliseconds += stopwatch.ElapsedMilliseconds;
		}
		catch (Exception ex)
		{
			stopwatch.Stop();
			await MarkFailedAsync(markerRepository, step, tenantId, options.Profile, stopwatch.ElapsedMilliseconds, ex);
			throw;
		}
		finally
		{
			_logger.LogInformation(
				"[SeederTiming] {SeederName} completed for TenantId={TenantId} in {ElapsedMs} ms",
				step.Name,
				tenantId,
				stopwatch.ElapsedMilliseconds);
		}
	}

	private static async Task<bool> ShouldSkipByMarkerAsync(
		IRepository<Seeders.SeedExecutionMarker, Guid> markerRepository,
		Seeders.IShikshaMitraSeederStep step,
		Guid? tenantId,
		Seeders.ShikshaMitraSeederOptions options)
	{
		if (!step.UseCompletionMarker || !options.UseCompletionMarkers || options.ForceRerun)
		{
			return false;
		}

		return await markerRepository.AnyAsync(x =>
			x.TenantId == tenantId &&
			x.SeederName == step.Name &&
			x.Version == step.Version &&
			x.Profile == options.Profile &&
			x.Status == Seeders.SeedExecutionStatus.Completed);
	}

	private async Task MarkStartedAsync(
		IRepository<Seeders.SeedExecutionMarker, Guid> markerRepository,
		Seeders.IShikshaMitraSeederStep step,
		Guid? tenantId,
		string profile)
	{
		if (!step.UseCompletionMarker)
		{
			return;
		}

		var marker = await FindMarkerAsync(markerRepository, step, tenantId, profile);
		if (marker == null)
		{
			marker = new Seeders.SeedExecutionMarker(Guid.NewGuid(), tenantId, step.Name, step.Version, profile);
			await markerRepository.InsertAsync(marker, autoSave: true);
			return;
		}

		marker.MarkStarted();
		await markerRepository.UpdateAsync(marker, autoSave: true);
	}

	private static async Task MarkCompletedAsync(
		IRepository<Seeders.SeedExecutionMarker, Guid> markerRepository,
		Seeders.IShikshaMitraSeederStep step,
		Guid? tenantId,
		string profile,
		long elapsedMilliseconds)
	{
		if (!step.UseCompletionMarker)
		{
			return;
		}

		var marker = await FindMarkerAsync(markerRepository, step, tenantId, profile);
		if (marker == null)
		{
			marker = new Seeders.SeedExecutionMarker(Guid.NewGuid(), tenantId, step.Name, step.Version, profile);
			marker.MarkCompleted(elapsedMilliseconds);
			await markerRepository.InsertAsync(marker, autoSave: true);
			return;
		}

		marker.MarkCompleted(elapsedMilliseconds);
		await markerRepository.UpdateAsync(marker, autoSave: true);
	}

	private static async Task MarkFailedAsync(
		IRepository<Seeders.SeedExecutionMarker, Guid> markerRepository,
		Seeders.IShikshaMitraSeederStep step,
		Guid? tenantId,
		string profile,
		long elapsedMilliseconds,
		Exception exception)
	{
		if (!step.UseCompletionMarker)
		{
			return;
		}

		var marker = await FindMarkerAsync(markerRepository, step, tenantId, profile);
		if (marker == null)
		{
			return;
		}

		marker.MarkFailed(elapsedMilliseconds, exception.ToString());
		await markerRepository.UpdateAsync(marker, autoSave: true);
	}

	private static Task<Seeders.SeedExecutionMarker?> FindMarkerAsync(
		IRepository<Seeders.SeedExecutionMarker, Guid> markerRepository,
		Seeders.IShikshaMitraSeederStep step,
		Guid? tenantId,
		string profile) =>
		markerRepository.FirstOrDefaultAsync(x =>
			x.TenantId == tenantId &&
			x.SeederName == step.Name &&
			x.Version == step.Version &&
			x.Profile == profile);

	private sealed class SeederRunSummary
	{
		public int RanCount { get; set; }
		public int MarkerSkippedCount { get; set; }
		public int GuardSkippedCount { get; set; }
		public int ProfileSkippedCount { get; set; }
		public long ElapsedMilliseconds { get; set; }
	}
}
