using System;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.Timing;
using Volo.Abp.Uow;
using Volo.Abp.Linq;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

public class PayrollPreviewBackgroundJob(
	IRepository<PayrollPreviewRun, Guid> runs,
	IRepository<PayrollPreviewEmployee, Guid> results,
	IdentityUserManager users,
	ICurrentTenant tenant,
	ICurrentPrincipalAccessor principalAccessor,
	IPermissionChecker permissions,
	IUnitOfWorkManager uows,
	IClock clock,
	IBackgroundJobManager jobs,
	IPayrollInputRevisionStore revisions,
	IAsyncQueryableExecuter executor,
	PayrollScopedEmployeeResolver scopeResolver,
	PayrollIneligibleRowCleaner ineligibleRowCleaner,
	PayrollSnapshotRefresher refresher,
	PayrollRunSnapshotStore snapshots,
	PayrollSnapshotLockProvider monthLocks,
	ILogger<PayrollPreviewBackgroundJob> logger) : AsyncBackgroundJob<PayrollPreviewJobArgs>, ITransientDependency
{
	public override async Task ExecuteAsync(PayrollPreviewJobArgs args)
	{
		using var tenantScope = tenant.Change(args.TenantId);
		PayrollPreviewRun run;
		ClaimsPrincipal principal;
		using (var lookup = uows.Begin(requiresNew: true, isTransactional: false))
		{
			run = await runs.GetAsync(args.RunId);
			if (run.Status is "SUCCEEDED" or "FAILED" or "SUPERSEDED") return;
			var user = await users.FindByIdAsync(run.RequestedBy.ToString());
			if (user == null || !user.IsActive || user.TenantId != args.TenantId)
			{
				await FinishFailedAttemptAsync(run.Id, null, HRErrorCodes.PayrollPreviewNotFound, retry: false);
				return;
			}
			var claims = new System.Collections.Generic.List<Claim>
			{
				new(AbpClaimTypes.UserId, user.Id.ToString()), new(AbpClaimTypes.UserName, user.UserName)
			};
			if (args.TenantId.HasValue) claims.Add(new Claim(AbpClaimTypes.TenantId, args.TenantId.Value.ToString()));
			claims.AddRange((await users.GetRolesAsync(user)).Select(role => new Claim(AbpClaimTypes.Role, role)));
			principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "PayrollBackgroundJob", AbpClaimTypes.UserName, AbpClaimTypes.Role));
			await lookup.CompleteAsync();
		}
		using var principalScope = principalAccessor.Change(principal);
		int? ownedAttempt = null;
		try
		{
			var (_, _, lockMonth) = PayrollRunRules.ParseMonth(run.Month);
			await monthLocks.WithMonthLockAsync(lockMonth, async checkLease =>
			{
				using (var start = uows.Begin(requiresNew: true, isTransactional: true))
				{
					if (!await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_RunPayroll.View))
						throw new BusinessException(HRErrorCodes.PayrollPreviewNotFound);
					await revisions.LockAsync();
					run = await runs.GetAsync(args.RunId);
					if (run.IsTerminal) return true;
					if (run.Attempts >= 3)
					{
						run.Fail(HRErrorCodes.PayrollPreviewFailed, clock.Now);
						await runs.UpdateAsync(run, autoSave: true);
						await start.CompleteAsync();
						return true;
					}
					var (cleanupYear, cleanupMonth, cleanupMonthKey) = PayrollRunRules.ParseMonth(run.Month);
					var allScopedEmployees = await scopeResolver.GetScopedEmployeesAsync(false);
					var eligibleEmployees = await scopeResolver.GetScopedPayrollEmployeesAsync(cleanupYear, cleanupMonth, false);
					if (PayrollPreviewRunAppService.ScopeFingerprint(eligibleEmployees.Select(x => x.Id)) != run.ScopeFingerprint)
						throw new BusinessException(HRErrorCodes.PayrollPreviewStale);
					await ineligibleRowCleaner.CleanupAsync(cleanupMonthKey, allScopedEmployees, eligibleEmployees);
					// Cleanup can remove obsolete input adjustments. Capture the revision after that work commits with this start.
					run.Start(await revisions.ReadAsync(), clock.Now);
					ownedAttempt = run.Attempts;
					await runs.UpdateAsync(run, autoSave: true);
					await start.CompleteAsync();
				}
				using var calculation = uows.Begin(requiresNew: true, isTransactional: true);
				var (year, month, internalMonth) = PayrollRunRules.ParseMonth(run.Month);
				var scoped = await scopeResolver.GetScopedPayrollEmployeesAsync(year, month, false);
				if (PayrollPreviewRunAppService.ScopeFingerprint(scoped.Select(x => x.Id)) != run.ScopeFingerprint)
					throw new BusinessException(HRErrorCodes.PayrollPreviewStale);
				if (await revisions.ReadAsync() != run.InputRevision) throw new BusinessException(HRErrorCodes.PayrollPreviewStale);
				await refresher.RefreshAsync(internalMonth, year, month, run.AcademicYearId, scoped, false,
					completed => ReportProgressAsync(run.Id, ownedAttempt.Value, completed),
					async () =>
					{
						if (await revisions.LockAsync() != run.InputRevision)
							throw new BusinessException(HRErrorCodes.PayrollPreviewStale);
						// Query scalar state to avoid a tracked entity hiding concurrent invalidation.
						var state = await executor.FirstOrDefaultAsync((await runs.GetQueryableAsync())
							.Where(x => x.Id == run.Id).Select(x => new { x.Status, x.Attempts }));
						if (state?.Status != "RUNNING" || state.Attempts != run.Attempts)
							throw new BusinessException(HRErrorCodes.PayrollPreviewStale);
					});
				var ids = scoped.Select(x => x.Id).ToHashSet();
				var items = (await snapshots.GetByMonthAsync(internalMonth)).Where(x => ids.Contains(x.EmployeeId)).Select(PayrollRunProjection.ToItem).ToList();
				if (items.Count != ids.Count) throw new BusinessException(HRErrorCodes.PayrollPreviewFailed);
				await results.InsertManyAsync(items.Select(item => new PayrollPreviewEmployee(Guid.NewGuid(), tenant.Id,
					run.Id, item.EmployeeId, item.EmployeeName, item.EmployeeIdentifier, JsonSerializer.Serialize(item))), autoSave: true);
				if (await revisions.LockAsync() != run.InputRevision) throw new BusinessException(HRErrorCodes.PayrollPreviewStale);
				await checkLease();
				var current = await runs.GetAsync(run.Id);
				if (current.Attempts != run.Attempts || current.Status != "RUNNING") throw new BusinessException(HRErrorCodes.PayrollPreviewStale);
				current.Complete(JsonSerializer.Serialize(PayrollReadinessProjector.BuildSummary(internalMonth, items, items.Any(x => x.IsLocked))), clock.Now);
				await runs.UpdateAsync(current, autoSave: true);
				await calculation.CompleteAsync();
				return true;
			});
		}
		catch (BusinessException ex) when (ex.Code == HRErrorCodes.PayrollMonthLockBusy)
		{
			// Let ABP retry a genuinely busy writer. Do not acknowledge a still-running job.
			throw;
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Payroll preview {RunId} failed", args.RunId);
			var code = ex is BusinessException business ? business.Code ?? HRErrorCodes.PayrollPreviewFailed : HRErrorCodes.PayrollPreviewFailed;
			await FinishFailedAttemptAsync(args.RunId, ownedAttempt, code, retry: ownedAttempt.HasValue && code != HRErrorCodes.PayrollPreviewNotFound);
		}
	}

	private async Task ReportProgressAsync(Guid id, int attempt, int completed)
	{
		using var progress = uows.Begin(requiresNew: true, isTransactional: true);
		await revisions.LockAsync();
		var run = await runs.GetAsync(id);
		run.ReportProgress(attempt, completed);
		await runs.UpdateAsync(run, autoSave: true);
		await progress.CompleteAsync();
	}

	private async Task FinishFailedAttemptAsync(Guid id, int? attempt, string code, bool retry)
	{
		using var failure = uows.Begin(requiresNew: true, isTransactional: true);
		await revisions.LockAsync();
		var run = await runs.GetAsync(id);
		// A late failure from an expired worker must never overwrite its replacement.
		if (run.IsTerminal || (attempt.HasValue ? run.Attempts != attempt.Value || run.Status != "RUNNING" : run.Status != "QUEUED"))
			return;
		if (retry && run.Attempts < 3)
		{
			run.Retry(attempt!.Value, code);
			await runs.UpdateAsync(run, autoSave: true);
			await jobs.EnqueueAsync(new PayrollPreviewJobArgs { TenantId = tenant.Id, RunId = id }, delay: TimeSpan.FromSeconds(5));
		}
		else
		{
			run.Fail(code, clock.Now);
			await runs.UpdateAsync(run, autoSave: true);
		}
		await failure.CompleteAsync();
	}
}