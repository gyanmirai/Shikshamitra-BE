using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Setup;
using ShikshaMitra.Library.Shared;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.EntityFrameworkCore;

public partial class ShikshaMitraDbContext
{
	public DbSet<PayrollPreviewRun> PayrollPreviewRuns { get; set; }
	public DbSet<PayrollPreviewEmployee> PayrollPreviewEmployees { get; set; }
	public DbSet<PayrollInputRevision> PayrollInputRevisions { get; set; }
	public DbSet<PayrollApprovalSettings> PayrollApprovalSettings { get; set; }
	public DbSet<PayrollSubmission> PayrollSubmissions { get; set; }
	public DbSet<PayrollSubmissionEmployee> PayrollSubmissionEmployees { get; set; }
	public DbSet<PayrollEmployeeClaim> PayrollEmployeeClaims { get; set; }
	public DbSet<PayrollCommandReceipt> PayrollCommandReceipts { get; set; }

	public override int SaveChanges(bool acceptAllChangesOnSuccess)
	{
		ChangeTracker.DetectChanges();
		LibraryBorrowerScopeEvidenceGuard.Validate(this);
		ShikshaMitra.Library.Receiving.LibraryReceivingEvidenceGuard.Validate(this);
		ShikshaMitra.Library.Receiving.LibraryStockImportEvidenceGuard.Validate(this);
		ShikshaMitra.Library.InventoryVerification.LibraryStockVerificationEvidenceGuard.Validate(this);
		using var authorizationScopeChange = InvalidateChangedAuthorizationScope();
		var changes = ReadInputChanges();
		if (changes.Length == 0) return base.SaveChanges(acceptAllChangesOnSuccess);
		using var transaction = Database.CurrentTransaction == null ? Database.BeginTransaction() : null;
		foreach (var change in changes)
		{
			if (change.Payroll) Database.ExecuteSqlInterpolated(InvalidatePayrollInputs(change.Tenant));
			if (change.HrSetup) Database.ExecuteSqlInterpolated(InvalidateHrSetup(change.Tenant!.Value));
		}
		var result = base.SaveChanges(acceptAllChangesOnSuccess);
		transaction?.Commit();
		return result;
	}

	public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
	{
		ChangeTracker.DetectChanges();
		LibraryBorrowerScopeEvidenceGuard.Validate(this);
		ShikshaMitra.Library.Receiving.LibraryReceivingEvidenceGuard.Validate(this);
		ShikshaMitra.Library.Receiving.LibraryStockImportEvidenceGuard.Validate(this);
		ShikshaMitra.Library.InventoryVerification.LibraryStockVerificationEvidenceGuard.Validate(this);
		using var authorizationScopeChange = InvalidateChangedAuthorizationScope();
		var changes = ReadInputChanges();
		if (changes.Length == 0) return await SaveSubmissionChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
		// The fence and the input write must commit together, including callers without an ABP transaction.
		await using var transaction = Database.CurrentTransaction == null ? await Database.BeginTransactionAsync(cancellationToken) : null;
		foreach (var change in changes)
		{
			if (change.Payroll) await Database.ExecuteSqlInterpolatedAsync(InvalidatePayrollInputs(change.Tenant), cancellationToken);
			if (change.HrSetup) await Database.ExecuteSqlInterpolatedAsync(InvalidateHrSetup(change.Tenant!.Value), cancellationToken);
		}
		var result = await SaveSubmissionChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
		if (transaction != null) await transaction.CommitAsync(cancellationToken);
		return result;
	}

	private (Guid? Tenant, bool Payroll, bool HrSetup)[] ReadInputChanges() => ChangeTracker.Entries()
		.Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
		.Select(entry => new
		{
			Tenant = entry.Entity is IMultiTenant scoped ? scoped.TenantId : CurrentTenant.Id,
			Payroll = IsPayrollInput(entry.Entity, entry.Metadata.ClrType),
			HrSetup = IsHrSetupInput(entry),
			Entry = entry
		})
		.Where(change => (change.Payroll || (change.HrSetup && change.Tenant.HasValue)) && !IsAdvisorySetupReviewOnly(change.Entry))
		.GroupBy(change => change.Tenant).OrderBy(group => group.Key)
		.Select(group => (group.Key, group.Any(change => change.Payroll), group.Key.HasValue && group.Any(change => change.HrSetup)))
		.ToArray();

	private static FormattableString InvalidatePayrollInputs(Guid? tenantId) => $"""
		INSERT INTO "HR_PayrollInputRevisions" ("Id", "TenantId", "Revision") VALUES ({tenantId ?? Guid.Empty}, {tenantId}, {Guid.NewGuid()})
		ON CONFLICT ("Id") DO UPDATE SET "Revision" = EXCLUDED."Revision"
		""";

	internal static bool IsPayrollInput(object entity, Type? entityType = null)
	{
		if (entity is PayrollAdjustment adjustment) return !adjustment.IsSystemGenerated;
		if (entity is HrSetupConfigurationRevision or HrSetupReadinessSnapshot) return false;
		var ns = (entityType ?? entity.GetType()).Namespace ?? "";
		return (ns.StartsWith("ShikshaMitra.HR.", StringComparison.Ordinal)
			&& !ns.StartsWith("ShikshaMitra.HR.Payroll.RunPayroll", StringComparison.Ordinal)
			&& !ns.StartsWith("ShikshaMitra.HR.Payroll.SalarySlips", StringComparison.Ordinal)
			&& !ns.StartsWith("ShikshaMitra.HR.Audit", StringComparison.Ordinal)
			&& !ns.StartsWith("ShikshaMitra.HR.Reports", StringComparison.Ordinal)
			&& !ns.StartsWith("ShikshaMitra.HR.Dashboard", StringComparison.Ordinal))
			|| ns.StartsWith("ShikshaMitra.TenantSettings.AcademicYears", StringComparison.Ordinal)
			|| ns.StartsWith("ShikshaMitra.Security.UserScope", StringComparison.Ordinal)
			|| ns.StartsWith("ShikshaMitra.Groups", StringComparison.Ordinal);
	}
}
