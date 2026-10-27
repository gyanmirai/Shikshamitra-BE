using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.HR.Payroll.RunPayroll;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.EntityFrameworkCore;

public partial class ShikshaMitraDbContext
{
	public DbSet<PayrollPreviewRun> PayrollPreviewRuns { get; set; }
	public DbSet<PayrollPreviewEmployee> PayrollPreviewEmployees { get; set; }
	public DbSet<PayrollInputRevision> PayrollInputRevisions { get; set; }

	public override int SaveChanges(bool acceptAllChangesOnSuccess)
	{
		ChangeTracker.DetectChanges();
		using var authorizationScopeChange = InvalidateChangedAuthorizationScope();
		var tenants = ChangeTracker.Entries().Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
			.Where(x => IsPayrollInput(x.Entity, x.Metadata.ClrType) && !IsAdvisorySetupReviewOnly(x)).Select(x => x.Entity is IMultiTenant scoped ? scoped.TenantId : CurrentTenant.Id).Distinct().ToList();
		if (tenants.Count == 0) return base.SaveChanges(acceptAllChangesOnSuccess);
		using var transaction = Database.CurrentTransaction == null ? Database.BeginTransaction() : null;
		foreach (var tenantId in tenants.OrderBy(x => x))
		{
			var id = tenantId ?? Guid.Empty;
			var revision = Guid.NewGuid();
			Database.ExecuteSqlInterpolated($"INSERT INTO \"HR_PayrollInputRevisions\" (\"Id\", \"TenantId\", \"Revision\") VALUES ({id}, {tenantId}, {revision}) ON CONFLICT (\"Id\") DO UPDATE SET \"Revision\" = EXCLUDED.\"Revision\"");
		}
		var result = base.SaveChanges(acceptAllChangesOnSuccess);
		transaction?.Commit();
		return result;
	}

	public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
	{
		ChangeTracker.DetectChanges();
		using var authorizationScopeChange = InvalidateChangedAuthorizationScope();
		var tenants = ChangeTracker.Entries().Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
			.Where(x => IsPayrollInput(x.Entity, x.Metadata.ClrType) && !IsAdvisorySetupReviewOnly(x)).Select(x => x.Entity is IMultiTenant scoped ? scoped.TenantId : CurrentTenant.Id).Distinct().ToList();
		if (tenants.Count == 0) return await SaveSubmissionChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
		// The fence and the input write must commit together, including callers without an ABP transaction.
		await using var transaction = Database.CurrentTransaction == null ? await Database.BeginTransactionAsync(cancellationToken) : null;
		foreach (var tenantId in tenants.OrderBy(x => x))
		{
			var id = tenantId ?? Guid.Empty;
			var revision = Guid.NewGuid();
			await Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"HR_PayrollInputRevisions\" (\"Id\", \"TenantId\", \"Revision\") VALUES ({id}, {tenantId}, {revision}) ON CONFLICT (\"Id\") DO UPDATE SET \"Revision\" = EXCLUDED.\"Revision\"", cancellationToken);
		}
		var result = await SaveSubmissionChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
		if (transaction != null) await transaction.CommitAsync(cancellationToken);
		return result;
	}

	internal static bool IsPayrollInput(object entity, Type? entityType = null)
	{
		if (entity is PayrollAdjustment adjustment) return !adjustment.IsSystemGenerated;
		var ns = (entityType ?? entity.GetType()).Namespace ?? "";
		return (ns.StartsWith("ShikshaMitra.HR.", StringComparison.Ordinal)
			&& !ns.StartsWith("ShikshaMitra.HR.Payroll.RunPayroll", StringComparison.Ordinal)
			&& !ns.StartsWith("ShikshaMitra.HR.Payroll.SalarySlips", StringComparison.Ordinal)
			&& !ns.StartsWith("ShikshaMitra.HR.Audit", StringComparison.Ordinal)
			&& !ns.StartsWith("ShikshaMitra.HR.Dashboard", StringComparison.Ordinal))
			|| ns.StartsWith("ShikshaMitra.TenantSettings.AcademicYears", StringComparison.Ordinal)
			|| ns.StartsWith("ShikshaMitra.Security.UserScope", StringComparison.Ordinal)
			|| ns.StartsWith("ShikshaMitra.Groups", StringComparison.Ordinal);
	}
}
