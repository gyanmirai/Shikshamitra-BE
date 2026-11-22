using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Audit;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.Timing;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

public class PayrollSectionReviewLockGuard(
	PayrollApprovalAccess access,
	IRepository<PayrollSubmission, Guid> submissions,
	IRepository<PayrollSubmissionEmployee, Guid> submissionRows,
	IRepository<PayrollEmployeeClaim, Guid> claims,
	IAsyncQueryableExecuter queries,
	IClock clock,
	HrAuditWriter audit) : ITransientDependency
{
	internal async Task<SectionReviewLockPlan> PrepareAsync(RunPayrollRequest request, string month,
		IReadOnlyCollection<Guid> employeeIds)
	{
		var tenantId = access.TenantId;
		var reason = request.SectionReviewOverrideReason?.Trim();
		if (reason?.Length > RunPayrollRequest.MaxSectionReviewOverrideReasonLength)
			throw new BusinessException("PAYROLL_SECTION_REVIEW_OVERRIDE_REASON_INVALID");

		var batches = (await submissions.GetQueryableAsync()).Where(x => x.TenantId == tenantId && x.Month == month);
		var rows = (await submissionRows.GetQueryableAsync()).Where(x => x.TenantId == tenantId);
		var selected = await queries.ToListAsync(
			from batch in batches
			join row in rows on batch.Id equals row.SubmissionId
			where Enumerable.Contains(employeeIds, row.EmployeeId) &&
				(batch.State == PayrollSubmission.Pending || (batch.StageId.HasValue &&
					(batch.State == PayrollSubmission.Returned || batch.State == PayrollSubmission.Reviewed)))
			select new { Batch = batch, row.EmployeeId });

		var pending = selected.Where(x => x.Batch.State == PayrollSubmission.Pending).Select(x => x.Batch).DistinctBy(x => x.Id).ToList();
		if (pending.Any(x => !x.StageId.HasValue)) throw new BusinessException("PAYROLL_SUBMISSION_REQUIRED");

		var returned = selected.GroupBy(x => x.EmployeeId)
			.Select(x => x.OrderByDescending(y => y.Batch.SubmittedAt).ThenByDescending(y => y.Batch.Id).First().Batch)
			.Where(x => x.State == PayrollSubmission.Returned).DistinctBy(x => x.Id).ToList();

		var pendingIds = pending.Select(x => x.Id).ToList();
		var pendingRows = await queries.ToListAsync(rows.Where(x => pendingIds.Contains(x.SubmissionId)));
		if (pendingRows.Count > 0 && !await access.CoversAsync(access.ActorId, pendingRows.Select(x => x.EmployeeId).Distinct().ToList()))
			throw new AbpAuthorizationException();

		var activeClaims = await queries.ToListAsync((await claims.GetQueryableAsync())
			.Where(x => x.TenantId == tenantId && x.Month == month && x.SubmissionId.HasValue &&
				(pendingIds.Contains(x.SubmissionId.Value) || Enumerable.Contains(employeeIds, x.EmployeeId))));
		var expectedClaims = pendingRows.Select(x => (SubmissionId: (Guid?)x.SubmissionId, x.EmployeeId)).ToHashSet();
		var rowsByBatch = pendingRows.ToLookup(x => x.SubmissionId);
		if (pending.Any(x => rowsByBatch[x.Id].Count() != x.StaffCount) ||
			!expectedClaims.SetEquals(activeClaims.Select(x => (x.SubmissionId, x.EmployeeId))))
			throw new BusinessException("PAYROLL_SUBMISSION_CLAIM_MISMATCH");
		var hasOverride = pending.Count > 0 || returned.Count > 0;
		if (hasOverride && string.IsNullOrWhiteSpace(reason))
			throw new BusinessException("PAYROLL_SECTION_REVIEW_OVERRIDE_REQUIRED");

		return new(pending, returned, activeClaims, hasOverride ? reason : null);
	}

	internal async Task RecordOverridesAsync(SectionReviewLockPlan plan)
	{
		foreach (var batch in plan.PendingBatches)
		{
			batch.Resolve(batch.Version, PayrollSubmission.Withdrawn, clock.Now, "HrLockOverride", access.ActorId, plan.OverrideReason);
			await submissions.UpdateAsync(batch);
		}
		foreach (var claim in plan.ActiveClaims)
		{
			claim.Release(claim.SubmissionId!.Value);
			await claims.UpdateAsync(claim);
		}
		foreach (var batch in plan.PendingBatches.Concat(plan.ReturnedBatches))
		{
			await audit.WriteAsync(batch.AcademicYearId, "payroll_submission", batch.Id, batch.Month,
				"hr-lock-override", "HR locked payroll with an outstanding section review.", plan.OverrideReason,
				context: new { batch.Version, batch.StaffCount, batch.StageId, batch.ReviewerRoleId, batch.ReasonCode });
		}
	}
}

internal sealed record SectionReviewLockPlan(
	IReadOnlyList<PayrollSubmission> PendingBatches,
	IReadOnlyList<PayrollSubmission> ReturnedBatches,
	IReadOnlyList<PayrollEmployeeClaim> ActiveClaims,
	string? OverrideReason)
{
	internal int OverrideBatchCount => PendingBatches.Count + ReturnedBatches.Count;
}
