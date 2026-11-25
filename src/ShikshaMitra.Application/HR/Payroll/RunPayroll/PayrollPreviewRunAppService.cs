using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Payroll.SalarySlips;
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
	IRepository<PayrollEmployeeClaim, Guid> claims,
	IRepository<PayrollSubmission, Guid> submissions,
	IRepository<PayrollCommandReceipt, Guid> receipts,
	IRepository<PayslipDocument, Guid> documents,
	PayrollMonthCoverageReader coverage,
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
		const string command = "start-preview";
		var actor = CurrentUser.Id!.Value;
		var requestFingerprint = PayrollSubmissionEvidence.Fingerprint(new { canonicalMonth, academicYear, fingerprint, request.Refresh });
		if (request.CommandKey.HasValue)
		{
			if (request.CommandKey == Guid.Empty) throw new BusinessException("PAYROLL_PREVIEW_COMMAND_INVALID");
			var receipt = await receipts.FindAsync(x => x.ActorId == actor && x.Command == command && x.Key == request.CommandKey.Value);
			if (receipt != null)
			{
				if (receipt.Fingerprint != requestFingerprint) throw new BusinessException("PAYROLL_COMMAND_KEY_CONFLICT");
				return await ReadStatusAsync(await GetAuthorizedRunAsync(receipt.ResultId));
			}
		}
		async Task RecordAsync(PayrollPreviewRun run, bool reused)
		{
			if (request.CommandKey is { } key && CurrentTenant.Id is { } tenant)
				await receipts.InsertAsync(new PayrollCommandReceipt(GuidGenerator.Create(), tenant, actor,
					command, key, requestFingerprint, run.Id, JsonSerializer.Serialize(new { Reused = reused })));
		}
		var latest = await AsyncExecuter.FirstOrDefaultAsync(await GetScopedRunsAsync(canonicalMonth, academicYear, fingerprint));
		if (latest != null && !latest.HasCurrentCalculationVersion && (latest.Status is "QUEUED" or "RUNNING"))
		{
			latest.Supersede(Clock.Now);
			await runs.UpdateAsync(latest);
		}
		if (latest != null && latest.HasCurrentCalculationVersion && ((latest.Status is "QUEUED" or "RUNNING") ||
			(!request.Refresh && latest.Status == "SUCCEEDED" && latest.InputRevision == revision)))
		{
			await RecordAsync(latest, reused: true);
			await uow.CompleteAsync();
			return ToStatus(latest, revision);
		}
		var run = new PayrollPreviewRun(GuidGenerator.Create(), CurrentTenant.Id, CurrentUser.Id!.Value,
			canonicalMonth, academicYear, fingerprint, scoped.Count, Clock.Now);
		await runs.InsertAsync(run, autoSave: true);
		await jobs.EnqueueAsync(new PayrollPreviewJobArgs { TenantId = CurrentTenant.Id, RunId = run.Id });
		await RecordAsync(run, reused: false);
		await uow.CompleteAsync();
		return ToStatus(run, revision);
	}

	public virtual async Task<PayrollPreviewLookupDto> GetCurrentAsync(PayrollPreviewMonthInput input)
	{
		var (year, month, _) = PayrollRunRules.ParseMonth(input.Month);
		var academicYear = await academicYearResolver.ResolveAsync(input.AcademicYearId, year, month);
		var scopedIds = await scopeResolver.ReadScopedEligibleIdsAsync(year, month);
		var scopedRuns = await GetScopedRunsAsync($"{year:0000}-{month:00}", academicYear, ScopeFingerprint(scopedIds));
		var latest = await AsyncExecuter.FirstOrDefaultAsync(scopedRuns);
		return new PayrollPreviewLookupDto
		{
			CurrentRun = latest == null ? null : await ReadStatusAsync(latest)
		};
	}

	public virtual async Task<PayrollPreviewStatusDto> GetAsync(Guid id) =>
		await ReadStatusAsync(await GetAuthorizedRunAsync(id));

	public virtual async Task<RunPayrollResponse> GetRegisterAsync(PayrollRegisterInput input)
	{
		ValidatePage(input);
		if (input.Sort is not (null or "employeeName" or "payableDays" or "netPay" or "netPayDelta") ||
			input.ReviewState is not (null or "PENDING" or "REVIEWED" or "SUBMITTED" or "LOCKED") ||
			input.IssueClass is not (null or "CLEAN" or "CRITICAL" or "HAS_ISSUES"))
			throw new BusinessException("PAYROLL_REGISTER_CRITERIA_INVALID");
		var (year, month, internalMonth) = PayrollRunRules.ParseMonth(input.Month);
		var canonicalMonth = $"{year:0000}-{month:00}";
		var academicYear = await academicYearResolver.ResolveAsync(input.AcademicYearId, year, month);
		var scopedStaff = await scopeResolver.GetScopedEmployeeQueryAsync(false);
		var scopedIds = scopedStaff.Select(x => x.Id);
		var eligibleIds = await PayrollScopedEmployeeResolver.ReadEligibleIdsAsync(scopedStaff, year, month, AsyncExecuter);
		var scopedRuns = await GetScopedRunsAsync(canonicalMonth, academicYear, ScopeFingerprint(eligibleIds));
		var workingRun = await AsyncExecuter.FirstOrDefaultAsync(scopedRuns.Where(x => x.Status == "SUCCEEDED"));
		var issuedSlips = await coverage.GetIssuedAsync(canonicalMonth, scopedStaff);
		var issuedIdsQuery = issuedSlips.Select(x => x.EmployeeId);
		var monthSnapshots = (await snapshots.GetQueryableAsync()).Where(x => x.MonthYear == internalMonth);
		var lockedSnapshots = monthSnapshots.Where(x => x.IsLocked && issuedIdsQuery.Contains(x.EmployeeId));
		var finalized = lockedSnapshots;
		var legacyIssued =
			from slip in issuedSlips
			join staff in scopedStaff on slip.EmployeeId equals staff.Id
			where !lockedSnapshots.Any(x => x.EmployeeId == slip.EmployeeId)
			select new
			{
				slip.Id, slip.EmployeeId, EmployeeName = ((staff.FirstName ?? "") + " " + (staff.LastName ?? "")).Trim(),
				Code = staff.EmployeeCode ?? "", slip.PayableDays, slip.NetSalary, slip.TotalDeduction, slip.Status
			};
		var working = (await employees.GetQueryableAsync()).Where(x => workingRun != null && x.RunId == workingRun.Id
			&& scopedIds.Contains(x.EmployeeId) && !issuedIdsQuery.Contains(x.EmployeeId));
		var allWorking = working;
		var issueCode = input.IssueCode?.Trim();
		if (!string.IsNullOrEmpty(issueCode))
		{
			working = working.Where(x => Enumerable.Contains(x.RegisterIssueCodes, issueCode));
			finalized = finalized.Where(x => Enumerable.Contains(x.RegisterIssueCodes, issueCode));
		}
		var blockingCodes = PayrollLockPreconditionGuard.BlockingIssueCodes.ToArray();
		switch (input.IssueClass)
		{
			case "CLEAN":
				working = working.Where(x => !x.RegisterIssueCodes.Any());
				finalized = finalized.Where(x => !x.RegisterIssueCodes.Any());
				break;
			case "CRITICAL":
				working = working.Where(x => x.RegisterIssueCodes.Intersect(blockingCodes).Any());
				finalized = finalized.Where(x => x.RegisterIssueCodes.Intersect(blockingCodes).Any());
				break;
			case "HAS_ISSUES":
				working = working.Where(x => x.RegisterIssueCodes.Any() && !x.RegisterIssueCodes.Intersect(blockingCodes).Any());
				finalized = finalized.Where(x => x.RegisterIssueCodes.Any() && !x.RegisterIssueCodes.Intersect(blockingCodes).Any());
				break;
		}
		if (!string.IsNullOrEmpty(issueCode) || input.IssueClass is "CRITICAL" or "HAS_ISSUES")
			legacyIssued = legacyIssued.Where(_ => false);
		var reviewQuery = (await runReviews.GetQueryableAsync()).Where(x => workingRun != null && x.PreviewRunId == workingRun.Id);
		var activeClaims = (await claims.GetQueryableAsync()).Where(x => x.Month == canonicalMonth && x.SubmissionId.HasValue);
		var batches = await submissions.GetQueryableAsync();
		var submittedQuery = activeClaims.Where(x => batches.Any(batch => batch.Id == x.SubmissionId && batch.StageId == null));
		var displayRows = working.Select(x => new
			{
				x.EmployeeId, x.EmployeeName, Code = x.EmployeeCode, x.PayableDays, x.NetPay, x.NetPayDelta, Source = 0,
				State = submittedQuery.Any(claim => claim.EmployeeId == x.EmployeeId) ? "SUBMITTED" :
					reviewQuery.Any(review => review.EmployeeId == x.EmployeeId) ? "REVIEWED" : "PENDING"
			})
			.Concat(finalized.Select(x => new
			{
				x.EmployeeId, x.EmployeeName, Code = x.EmployeeIdentifier, PayableDays = (decimal?) x.PayableDays,
				NetPay = (decimal?) x.NetPay, x.NetPayDelta, Source = 1, State = "LOCKED"
			}))
			.Concat(legacyIssued.Select(x => new
			{
				x.EmployeeId, x.EmployeeName, x.Code, x.PayableDays,
				NetPay = (decimal?) x.NetSalary, NetPayDelta = (decimal?) null, Source = 2, State = "LOCKED"
			}));
		var search = input.Search?.Trim().ToLowerInvariant();
		if (!string.IsNullOrEmpty(search))
			displayRows = displayRows.Where(x => x.EmployeeName.ToLower().Contains(search) || x.Code.ToLower().Contains(search));
		if (input.ReviewState != null)
			displayRows = displayRows.Where(x => x.State == input.ReviewState);
		var ordered = (input.Sort, input.Descending) switch
		{
			("payableDays", true) => displayRows.OrderBy(x => x.PayableDays == null).ThenByDescending(x => x.PayableDays),
			("payableDays", false) => displayRows.OrderBy(x => x.PayableDays == null).ThenBy(x => x.PayableDays),
			("netPay", true) => displayRows.OrderBy(x => x.NetPay == null).ThenByDescending(x => x.NetPay),
			("netPay", false) => displayRows.OrderBy(x => x.NetPay == null).ThenBy(x => x.NetPay),
			("netPayDelta", true) => displayRows.OrderBy(x => x.NetPayDelta == null).ThenByDescending(x => x.NetPayDelta),
			("netPayDelta", false) => displayRows.OrderBy(x => x.NetPayDelta == null).ThenBy(x => x.NetPayDelta),
			(_, true) => displayRows.OrderByDescending(x => x.EmployeeName),
			_ => displayRows.OrderBy(x => x.EmployeeName)
		};
		var page = ordered.ThenBy(x => x.EmployeeId)
			.Skip(checked(input.Page * input.PageSize)).Take(input.PageSize);
		if (input.RowsOnly)
		{
			return new RunPayrollResponse
			{
				PreviewRunId = workingRun?.Id,
				CurrentPage = input.Page,
				PageSize = input.PageSize,
				RegisterRows = await AsyncExecuter.ToListAsync(page.Select(x => new PayrollRegisterRowDto
				{
					EmployeeId = x.EmployeeId,
					EmployeeName = x.EmployeeName,
					EmployeeIdentifier = x.Code,
					PayableDays = x.Source == 2 ? null : x.PayableDays,
					NetPay = x.NetPay,
					IsLocked = x.Source != 0
				}))
			};
		}

		var count = await AsyncExecuter.CountAsync(displayRows);
		var pageRows = await AsyncExecuter.ToListAsync(page.Select(x => new { x.EmployeeId, x.Source }));
		var issuedIds = (await AsyncExecuter.ToListAsync(issuedIdsQuery)).ToHashSet();
		var workingIds = pageRows.Where(x => x.Source == 0).Select(x => x.EmployeeId).ToArray();
		var finalizedIds = pageRows.Where(x => x.Source == 1).Select(x => x.EmployeeId).ToArray();
		var legacyIds = pageRows.Where(x => x.Source == 2).Select(x => x.EmployeeId).ToArray();

		var workingRows = workingIds.Length == 0 ? [] : await AsyncExecuter.ToListAsync(
			working.Where(x => Enumerable.Contains(workingIds, x.EmployeeId)));
		var pageReviews = workingIds.Length == 0 ? [] : (await AsyncExecuter.ToListAsync(
			reviewQuery.Where(x => Enumerable.Contains(workingIds, x.EmployeeId))))
			.ToDictionary(x => x.EmployeeId);
		var pageClaims = workingIds.Length == 0 ? [] : (await AsyncExecuter.ToListAsync(
			from claim in activeClaims
			join batch in batches on claim.SubmissionId equals (Guid?)batch.Id
			where Enumerable.Contains(workingIds, claim.EmployeeId)
			select new { claim.EmployeeId, SectionReview = batch.StageId.HasValue }))
			.ToDictionary(claim => claim.EmployeeId, claim => claim.SectionReview);
		var items = finalizedIds.Length == 0 ? new Dictionary<Guid, RunPayrollItem>()
			: (await AsyncExecuter.ToListAsync(finalized.Where(x => Enumerable.Contains(finalizedIds, x.EmployeeId))))
				.ToDictionary(x => x.EmployeeId, PayrollRunProjection.ToItem);
		var legacyRows = legacyIds.Length == 0 ? [] : await AsyncExecuter.ToListAsync(
			legacyIssued.Where(x => Enumerable.Contains(legacyIds, x.EmployeeId)));
		if (legacyRows.Count > 0)
		{
			var slipIds = legacyRows.Select(x => x.Id).ToList();
			var documentQuery = await documents.GetQueryableAsync();
			var currentDocuments = await AsyncExecuter.ToListAsync(documentQuery.Where(x => slipIds.Contains(x.SalarySlipId) &&
				!documentQuery.Any(other => other.SalarySlipId == x.SalarySlipId && other.Revision > x.Revision))
				.Select(x => new { x.SalarySlipId, x.FactsJson }));
			var factsBySlip = currentDocuments.ToDictionary(x => x.SalarySlipId, x => JsonSerializer.Deserialize<SalarySlipDto>(x.FactsJson));
			foreach (var row in legacyRows)
			{
				var facts = factsBySlip.GetValueOrDefault(row.Id);
				items.Add(row.EmployeeId, new RunPayrollItem
				{
					EmployeeId = row.EmployeeId, EmployeeName = facts?.EmployeeName ?? row.EmployeeName,
					EmployeeIdentifier = facts?.EmployeeCode ?? row.Code, IsLocked = true, ReviewStatus = ReviewStatuses.Locked,
					IsIssuedWithoutRegisterDetails = facts?.FullGrossSalary == null || facts.PayableDays == null || facts.PayrollBasisDays == null,
					FullGrossSalary = (double) (facts?.FullGrossSalary ?? 0m),
					GrossSalary = (double) (facts?.GrossSalary ?? 0m), NetPay = (double) row.NetSalary, TotalDeduction = (double) row.TotalDeduction,
					PayableDays = (double) (facts?.PayableDays ?? 0m), TotalWorkingDays = (double) (facts?.PayrollBasisDays ?? 0m)
				});
			}
		}
		foreach (var row in workingRows)
		{
			var item = JsonSerializer.Deserialize<RunPayrollItem>(row.ItemJson)!;
			item.IsLocked = false;
			var hasClaim = pageClaims.TryGetValue(row.EmployeeId, out var awaitingSectionReview);
			item.IsSubmitted = hasClaim && !awaitingSectionReview;
			item.HasPendingSectionReview = hasClaim && awaitingSectionReview;
			item.ReviewStatus = ReviewStatuses.Pending;
			item.ReviewedAt = null;
			item.ReviewedByUserId = null;
			if (pageReviews.TryGetValue(row.EmployeeId, out var review))
			{
				item.ReviewStatus = ReviewStatuses.Reviewed;
				item.ReviewedAt = review.ReviewedAt;
				item.ReviewedByUserId = review.ReviewedByUserId;
			}
			items.Add(row.EmployeeId, item);
		}

		var isCurrent = workingRun is { HasCurrentCalculationVersion: true }
			&& workingRun.InputRevision == await revisions.ReadAsync();
		var summary = workingRun?.SummaryJson is { } json
			? JsonSerializer.Deserialize<RunPayrollReadinessSummary>(json)!
			: new RunPayrollReadinessSummary { Month = canonicalMonth };
		summary.EmployeeCount = eligibleIds.Union(issuedIds).Count();
		summary.LockedCount = issuedIds.Count;
		summary.ReviewedCount = workingRun == null ? 0 :
			await AsyncExecuter.CountAsync(reviewQuery.Where(x => allWorking.Any(row => row.EmployeeId == x.EmployeeId)));
		summary.CanLock = isCurrent && summary.BlockingIssues.Count == 0 && summary.ReviewedCount > 0;
		return new RunPayrollResponse
		{
			PreviewRunId = workingRun?.Id,
			IsCurrent = isCurrent,
			IsGenerated = summary.EmployeeCount > 0 && summary.LockedCount == summary.EmployeeCount,
			TotalCount = count,
			CurrentPage = input.Page,
			PageSize = input.PageSize,
			TotalPages = (int) Math.Ceiling((double) count / input.PageSize),
			Items = pageRows.Select(x => x.EmployeeId).Where(items.ContainsKey).Select(id => items[id]).ToList(),
			Readiness = summary
		};
	}

	private async Task<PayrollPreviewStatusDto> ReadStatusAsync(PayrollPreviewRun run)
	{
		var status = ToStatus(run, await revisions.ReadAsync());
		status.IsCurrent = status.IsCurrent && await IsLatestCompletedRunAsync(run);
		if (!run.HasCurrentCalculationVersion && status.Status is "QUEUED" or "RUNNING")
			status.Status = "SUPERSEDED";
		if (run.Status != "SUCCEEDED")
		{
			var scopedRuns = await GetScopedRunsAsync(run.Month, run.AcademicYearId, run.ScopeFingerprint);
			status.LastCompletedRunId = await AsyncExecuter.FirstOrDefaultAsync(scopedRuns
				.Where(x => x.Status == "SUCCEEDED").Select(x => (Guid?) x.Id));
		}
		return status;
	}

	private async Task<IOrderedQueryable<PayrollPreviewRun>> GetScopedRunsAsync(string month, Guid? academicYearId, string fingerprint) =>
		(await runs.GetQueryableAsync())
			.Where(x => x.Month == month && x.AcademicYearId == academicYearId && x.ScopeFingerprint == fingerprint)
			.OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id);

	public virtual async Task<RunPayrollResponse> GetEmployeesAsync(Guid id, PayrollPreviewPageInput input)
	{
		ValidatePage(input);
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
			item.ReviewedByUserId = snapshot.IsLocked || reviewedIds.Contains(item.EmployeeId) ? snapshot.ReviewedByUserId : null;
			item.IsLocked = snapshot.IsLocked;
		}
		var isCurrent = run.HasCurrentCalculationVersion && run.Status == "SUCCEEDED"
			&& run.InputRevision == await revisions.ReadAsync() && await IsLatestCompletedRunAsync(run);
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

	private static void ValidatePage(PayrollPageInput input)
	{
		if (input.Page < 0 || input.PageSize is < 1 or > 100 || (long) input.Page * input.PageSize > int.MaxValue)
			throw new BusinessException(HRErrorCodes.PayrollInvalidPageSize);
	}

	internal async Task<PayrollPreviewRun> GetAuthorizedRunAsync(Guid id)
	{
		var run = await runs.FindAsync(id) ?? throw new BusinessException(HRErrorCodes.PayrollPreviewNotFound);
		var (year, month, _) = PayrollRunRules.ParseMonth(run.Month);
		await academicYearResolver.ResolveAsync(run.AcademicYearId, year, month);
		var scopedIds = await scopeResolver.ReadScopedEligibleIdsAsync(year, month);
		if (run.ScopeFingerprint != ScopeFingerprint(scopedIds)) throw new BusinessException(HRErrorCodes.PayrollPreviewNotFound);
		return run;
	}

	internal static string ScopeFingerprint(System.Collections.Generic.IEnumerable<Guid> ids) =>
		Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join(",", ids.OrderBy(x => x)))));

	internal async Task<bool> IsLatestCompletedRunAsync(PayrollPreviewRun run)
	{
		var query = await runs.GetQueryableAsync();
		var latestId = await AsyncExecuter.FirstOrDefaultAsync(query.Where(x => x.Month == run.Month
			&& x.AcademicYearId == run.AcademicYearId && x.ScopeFingerprint == run.ScopeFingerprint
			&& x.InputRevision == run.InputRevision && x.Status == "SUCCEEDED")
			.OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id).Select(x => x.Id));
		return latestId == run.Id;
	}

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
