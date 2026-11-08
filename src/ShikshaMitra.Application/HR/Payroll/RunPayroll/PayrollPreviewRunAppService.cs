using System;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

[RemoteService(false)]
[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_RunPayroll.View)]
public class PayrollPreviewRunAppService(
	IRepository<PayrollPreviewRun, Guid> runs,
	IRepository<PayrollPreviewEmployee, Guid> employees,
	IRepository<PayrollRunItemSnapshot, Guid> snapshots,
	IRepository<PayrollRunReview, Guid> runReviews,
	PayrollScopedEmployeeResolver scopeResolver,
	PayrollAcademicYearScopeResolver academicYearResolver,
	IPayrollInputRevisionStore revisions,
	IBackgroundJobManager jobs) : ApplicationService, IPayrollPreviewRunAppService
{
	[UnitOfWork(IsDisabled = true)]
	public virtual async Task<PayrollPreviewStatusDto> StartAsync(StartPayrollPreviewRequest request)
	{
		using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
		var (year, month, _) = PayrollRunRules.ParseMonth(request.Month);
		var canonicalMonth = $"{year:0000}-{month:00}";
		var academicYear = await academicYearResolver.ResolveAsync(request.AcademicYearId, year, month);
		var scoped = await scopeResolver.GetScopedPayrollEmployeesAsync(year, month, false);
		var fingerprint = ScopeFingerprint(scoped.Select(x => x.Id));
		var revision = await revisions.LockAsync();
		var query = await runs.GetQueryableAsync();
		var latest = await AsyncExecuter.FirstOrDefaultAsync(query.Where(x => x.Month == canonicalMonth
			&& x.AcademicYearId == academicYear && x.ScopeFingerprint == fingerprint).OrderByDescending(x => x.CreatedAtUtc));
		if (latest != null && !latest.HasCurrentCalculationVersion && (latest.Status is "QUEUED" or "RUNNING"))
		{
			latest.Supersede(Clock.Now);
			await runs.UpdateAsync(latest);
		}
		if (latest != null && latest.HasCurrentCalculationVersion && ((latest.Status is "QUEUED" or "RUNNING") ||
			(!request.Refresh && latest.Status == "SUCCEEDED" && latest.InputRevision == revision)))
		{
			await uow.CompleteAsync();
			return ToStatus(latest, revision);
		}
		var run = new PayrollPreviewRun(GuidGenerator.Create(), CurrentTenant.Id, CurrentUser.Id!.Value,
			canonicalMonth, academicYear, fingerprint, scoped.Count, Clock.Now);
		await runs.InsertAsync(run, autoSave: true);
		await jobs.EnqueueAsync(new PayrollPreviewJobArgs { TenantId = CurrentTenant.Id, RunId = run.Id });
		await uow.CompleteAsync();
		return ToStatus(run, revision);
	}

	public virtual async Task<PayrollPreviewStatusDto> GetAsync(Guid id)
	{
		var run = await GetAuthorizedRunAsync(id);
		var status = ToStatus(run, await revisions.ReadAsync());
		if (run.Status != "SUCCEEDED")
		{
			status.LastCompletedRunId = await AsyncExecuter.FirstOrDefaultAsync((await runs.GetQueryableAsync())
				.Where(x => x.Month == run.Month && x.AcademicYearId == run.AcademicYearId &&
					x.ScopeFingerprint == run.ScopeFingerprint && x.Status == "SUCCEEDED")
				.OrderByDescending(x => x.CreatedAtUtc).Select(x => (Guid?) x.Id));
		}
		return status;
	}

	public virtual async Task<RunPayrollResponse> GetEmployeesAsync(Guid id, PayrollPreviewPageInput input)
	{
		if (input.Page < 0 || input.PageSize is < 1 or > 100 || (long) input.Page * input.PageSize > int.MaxValue)
			throw new BusinessException(HRErrorCodes.PayrollInvalidPageSize);
		var run = await GetAuthorizedRunAsync(id);
		if (run.Status != "SUCCEEDED" && !(run.Status == "SUPERSEDED" && run.SummaryJson != null))
			throw new BusinessException(HRErrorCodes.PayrollPreviewNotReady);
		var allResults = (await employees.GetQueryableAsync()).Where(x => x.RunId == id);
		var query = allResults;
		var search = input.Search?.Trim().ToLowerInvariant();
		if (!string.IsNullOrEmpty(search)) query = query.Where(x => x.EmployeeName.ToLower().Contains(search) || x.EmployeeCode.ToLower().Contains(search));
		var count = await AsyncExecuter.CountAsync(query);
		var rows = await AsyncExecuter.ToListAsync(query.OrderBy(x => x.EmployeeName).ThenBy(x => x.EmployeeId)
			.Skip(checked(input.Page * input.PageSize)).Take(input.PageSize));
		var summary = JsonSerializer.Deserialize<RunPayrollReadinessSummary>(run.SummaryJson!)!;
		var (_, _, internalMonth) = PayrollRunRules.ParseMonth(run.Month);
		var snapshotQuery = (await snapshots.GetQueryableAsync()).Where(x => x.MonthYear == internalMonth && allResults.Any(result => result.EmployeeId == x.EmployeeId));
		var generationReviews = (await runReviews.GetQueryableAsync()).Where(x => x.PreviewRunId == id);
		summary.ReviewedCount = await AsyncExecuter.CountAsync(snapshotQuery.Where(x => !x.IsLocked && generationReviews.Any(review => review.EmployeeId == x.EmployeeId)));
		summary.LockedCount = await AsyncExecuter.CountAsync(snapshotQuery.Where(x => x.IsLocked));
		var pageIds = rows.Select(x => x.EmployeeId).ToList();
		var reviews = (await AsyncExecuter.ToListAsync(snapshotQuery.Where(x => pageIds.Contains(x.EmployeeId))))
			.ToDictionary(x => x.EmployeeId);
		var items = rows.Select(x => JsonSerializer.Deserialize<RunPayrollItem>(x.ItemJson)!).ToList();
		var reviewedIds = (await AsyncExecuter.ToListAsync(generationReviews.Where(x => pageIds.Contains(x.EmployeeId)).Select(x => x.EmployeeId))).ToHashSet();
		foreach (var item in items)
		{
			if (!reviews.TryGetValue(item.EmployeeId, out var snapshot))
			{
				item.ReviewStatus = ReviewStatuses.Pending;
				item.ReviewedAt = null;
				item.ReviewedByUserId = null;
				continue;
			}
			item.ReviewStatus = snapshot.IsLocked || reviewedIds.Contains(item.EmployeeId) ? snapshot.ReviewStatus : ReviewStatuses.Pending;
			item.ReviewedAt = snapshot.IsLocked || reviewedIds.Contains(item.EmployeeId) ? snapshot.ReviewedAt : null;
			item.IsLocked = snapshot.IsLocked;
		}
		var isCurrent = run.HasCurrentCalculationVersion && run.Status == "SUCCEEDED" && run.InputRevision == await revisions.ReadAsync();
		summary.CanLock = isCurrent && summary.BlockingIssues.Count == 0 && summary.ReviewedCount > 0;
		return new RunPayrollResponse
		{
			PreviewRunId = id,
			IsCurrent = isCurrent,
			IsGenerated = summary.EmployeeCount > 0 && summary.LockedCount == summary.EmployeeCount,
			TotalCount = count,
			CurrentPage = input.Page,
			PageSize = input.PageSize,
			TotalPages = (int) Math.Ceiling((double) count / input.PageSize),
			Items = items,
			Readiness = summary
		};
	}

	internal async Task<PayrollPreviewRun> GetAuthorizedRunAsync(Guid id)
	{
		var run = await runs.FindAsync(id) ?? throw new BusinessException(HRErrorCodes.PayrollPreviewNotFound);
		var (year, month, _) = PayrollRunRules.ParseMonth(run.Month);
		await academicYearResolver.ResolveAsync(run.AcademicYearId, year, month);
		var scoped = await scopeResolver.GetScopedPayrollEmployeesAsync(year, month, false);
		if (run.ScopeFingerprint != ScopeFingerprint(scoped.Select(x => x.Id))) throw new BusinessException(HRErrorCodes.PayrollPreviewNotFound);
		return run;
	}

	internal static string ScopeFingerprint(System.Collections.Generic.IEnumerable<Guid> ids) =>
		Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join(",", ids.OrderBy(x => x)))));

	internal static PayrollPreviewStatusDto ToStatus(PayrollPreviewRun run, Guid revision) => new()
	{
		Id = run.Id,
		Month = run.Month,
		AcademicYearId = run.AcademicYearId,
		Status = run.Status,
		EmployeeCount = run.EmployeeCount,
		CompletedEmployees = run.CompletedEmployees,
		IsCurrent = run.HasCurrentCalculationVersion && run.Status == "SUCCEEDED" && run.InputRevision == revision,
		CreatedAtUtc = run.CreatedAtUtc,
		StartedAtUtc = run.StartedAtUtc,
		CompletedAtUtc = run.CompletedAtUtc,
		ErrorCode = run.ErrorCode,
		ErrorMessage = run.ErrorCode == null ? null :
			run.ErrorCode == HRErrorCodes.PayrollPreviewStale ? "Payroll inputs changed during calculation. Refresh and review the new results."
			: "Payroll calculation could not finish. Retry the calculation or contact support with the run reference."
	};
}
