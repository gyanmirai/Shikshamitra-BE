using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.TenantSettings.SchoolDocuments;
using ShikshaMitra.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Uow;

namespace ShikshaMitra.TenantSettings;

public class SchoolDocumentsTestFactory : ShikshaMitraApiTestFactory
{
	public string? HostContentRoot { get; set; }
	protected override bool InitializeTestData => false;
	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		base.ConfigureWebHost(builder);
		if (HostContentRoot != null) builder.UseContentRoot(HostContentRoot);
		builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection([
			new("Logging:LogLevel:Default", "Warning")]));
		builder.ConfigureServices(services => services.Configure<AbpDbContextOptions>(options => options.PreConfigure(context =>
			context.DbContextOptions.AddInterceptors(new SchoolDocumentSaveFailureInterceptor()))));
	}

	public async Task SeedTenantAsync(Guid id)
	{
		using var scope = Services.CreateScope();
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(null);
		using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var entity = await scope.ServiceProvider.GetRequiredService<ITenantManager>().CreateAsync("documents-" + id);
		typeof(Tenant).GetProperty(nameof(Tenant.Id))!.SetValue(entity, id);
		await scope.ServiceProvider.GetRequiredService<IRepository<Tenant, Guid>>().InsertAsync(entity);
		await unit.CompleteAsync();
	}

	public async Task<(int Documents, int Files, long Blobs)> StorageCountsAsync(Guid tenantId)
	{
		using var scope = Services.CreateScope();
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId);
		using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var db = await scope.ServiceProvider.GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
		var documentCount = await db.SchoolDocuments.IgnoreQueryFilters().CountAsync(x => x.TenantId == tenantId);
		var fileCount = await db.SchoolDocumentFiles.IgnoreQueryFilters().CountAsync(x => x.TenantId == tenantId);
		var blobs = await db.Database.SqlQueryRaw<long>("SELECT COUNT(*) AS \"Value\" FROM \"AbpBlobs\" WHERE \"TenantId\" = {0}", tenantId).SingleAsync();
		return (documentCount, fileCount, blobs);
	}
}

public class SchoolDocumentSaveFailureInterceptor : SaveChangesInterceptor
{
	public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
		InterceptionResult<int> result, CancellationToken cancellationToken = default)
	{
		if (eventData.Context?.ChangeTracker.Entries<SchoolDocumentFile>() is { } entries)
			foreach (var entry in entries)
				if (entry.State == EntityState.Added && entry.Entity.FileName == "rollback-failure.png")
					throw new InvalidOperationException("Synthetic attachment persistence failure.");
		return base.SavingChangesAsync(eventData, result, cancellationToken);
	}
}
