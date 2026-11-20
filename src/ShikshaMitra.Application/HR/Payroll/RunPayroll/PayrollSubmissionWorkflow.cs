using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.Audit;
using ShikshaMitra.HR.Payroll.SalarySlips;
using ShikshaMitra.HR.Payroll.Statutory;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using Permission = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.HR.PayrollManagement_RunPayroll;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

[RemoteService(false)]
public class PayrollSubmissionWorkflow(
	PayrollApprovalAccess access,
	PayrollSubmissionEvidence evidence,
	PayrollPreviewReviewGuard previews,
	PayrollAcademicYearScopeResolver academicYears,
	PayrollSnapshotLockProvider monthLocks,
	IPayrollInputRevisionStore revisions,
	IRepository<PayrollSubmission, Guid> submissions,
	IRepository<PayrollSubmissionEmployee, Guid> submissionRows,
	IRepository<PayrollEmployeeClaim, Guid> claims,
	IRepository<PayrollCommandReceipt, Guid> receipts,
	IRepository<PayrollPreviewEmployee, Guid> previewRows,
	IRepository<PayrollRunReview, Guid> reviews,
	IRepository<PayrollRunItemSnapshot, Guid> snapshots,
	ISalarySlipRepository salarySlips,
	PayrollLockBatchProcessor processor,
	PayslipIssuanceService issuance,
	EsiContinuityInvalidator continuity,
	HrAuditWriter audit) : ApplicationService
{
	internal async Task<List<PayrollSubmissionDto>> GetListAsync(PayrollPreviewMonthInput input)
	{
		var (year, month, period) = PayrollRunRules.ParseMonth(input.Month);
		var academicYear = await academicYears.ResolveAsync(input.AcademicYearId, year, month);
		var actor = access.ActorId;
		var manager = await access.CanManageAsync();
		var setting = await access.ReadSettingsAsync();
		var revision = await revisions.ReadAsync();
		var query = (await submissions.GetQueryableAsync()).Where(x => x.Month == period && x.AcademicYearId == academicYear &&
			(manager || x.SubmitterId == actor || x.ApproverId == actor));
		var visible = new List<PayrollSubmissionDto>();
		foreach (var batch in await AsyncExecuter.ToListAsync(query.OrderByDescending(x => x.SubmittedAt).ThenBy(x => x.Id)))
		{
			var rows = await ReadRowsAsync(batch);
			var roles = await ReadRolesAsync(batch, setting, rows);
			if (!roles.CanRead) continue;
			var stale = batch.State == PayrollSubmission.Pending && await ReadCurrentAsync(batch, rows, revision) == null;
			visible.Add(ToDto(batch, roles, setting, stale));
		}
		return visible;
	}

	internal async Task<PayrollSubmissionDetailDto> GetAsync(Guid id)
	{
		var batch = await GetBatchAsync(id);
		var rows = await ReadRowsAsync(batch);
		var setting = await access.ReadSettingsAsync();
		var roles = await ReadRolesAsync(batch, setting, rows);
		if (!roles.CanRead) throw new AbpAuthorizationException();
		var stale = batch.State == PayrollSubmission.Pending && await ReadCurrentAsync(batch, rows, await revisions.ReadAsync()) == null;
		var previewIds = rows.Select(x => x.PreviewEmployeeId).ToList();
		var saved = await AsyncExecuter.ToListAsync((await previewRows.GetQueryableAsync()).Where(x => previewIds.Contains(x.Id))
			.OrderBy(x => x.EmployeeName).ThenBy(x => x.EmployeeId));
		var items = saved.Select(x => JsonSerializer.Deserialize<RunPayrollItem>(x.ItemJson)!).ToList();
		return new(ToDto(batch, roles, setting, stale), items);
	}

	internal async Task<(string Month, List<PayrollVerifiedRow> Rows)> ReadPendingForExportAsync(Guid id, int version, Guid revision)
	{
		var batch = await GetBatchAsync(id);
		var rows = await ReadRowsAsync(batch);
		var roles = await ReadRolesAsync(batch, await access.ReadSettingsAsync(), rows);
		if (!roles.CanRead) throw new AbpAuthorizationException();
		if (batch.State != PayrollSubmission.Pending || batch.Version != version)
			throw new BusinessException("PAYROLL_REVIEW_SHEET_EXPIRED");
		var verified = await ReadCurrentAsync(batch, rows, revision)
			?? throw new BusinessException("PAYROLL_REVIEW_SHEET_EXPIRED");
		return (batch.Month, verified);
	}

	internal async Task<PayrollSubmissionDto> SubmitAsync(SubmitPayrollCommand command)
	{
		var (year, month, period) = PayrollRunRules.ParseMonth(command.Month);
		var ids = command.EmployeeIds.Order().ToList();
		if (command.CommandKey == Guid.Empty || ids.Count == 0 || ids.Contains(Guid.Empty) || ids.Distinct().Count() != ids.Count)
			throw new BusinessException("PAYROLL_SUBMISSION_SELECTION_INVALID");
		return await UnderFenceAsync(period, async revision =>
		{
			var actor = access.ActorId;
			if (!await access.CanAsync(actor, Permission.View, Permission.Submit) || !await access.CoversAsync(actor, ids))
				throw new AbpAuthorizationException();
			var academicYear = await academicYears.ResolveAsync(command.AcademicYearId, year, month);
			var fingerprint = PayrollSubmissionEvidence.Fingerprint(new
			{
				period, academicYear, ids, command.PreviewRunId, command.SelfFinalize,
				command.PreviousSubmissionId, command.ExpectedSettingsVersion
			});
			var recorded = await ReplayAsync("submit", command.CommandKey, fingerprint);
			if (recorded != null) return recorded;
			var setting = await access.ReadSettingsAsync();
			if (setting?.Mode != PayrollApprovalSettings.Active) throw new BusinessException("PAYROLL_WORKFLOW_NOT_ACTIVE");
			if (setting.Version != command.ExpectedSettingsVersion) throw new BusinessException("PAYROLL_SETTINGS_VERSION_CONFLICT");
			var approver = command.SelfFinalize ? actor : setting.ApproverId;
			if (approver == null || (command.SelfFinalize ? !setting.AllowSelfFinalization : approver == actor) ||
				!await access.CanApproveAsync(approver.Value, command.SelfFinalize) || !await access.CoversAsync(approver.Value, ids))
				throw new BusinessException("PAYROLL_APPROVER_UNAVAILABLE");
			if (command.PreviousSubmissionId is { } previousId)
			{
				var previous = await GetBatchAsync(previousId);
				var previousRows = (await ReadRowsAsync(previous)).Select(x => x.EmployeeId).ToHashSet();
				if (previous.SubmitterId != actor || previous.Month != period || previous.AcademicYearId != academicYear ||
					previous.State is not (PayrollSubmission.Returned or PayrollSubmission.Withdrawn) ||
					!ids.All(previousRows.Contains) || !await access.CoversAsync(actor, previousRows))
					throw new BusinessException("PAYROLL_RESUBMISSION_INVALID");
			}
			await previews.EnsureCurrentAsync(command.PreviewRunId, period, academicYear, ids);
			var ready = await evidence.ReadCurrentAsync(access.TenantId, command.PreviewRunId, revision, period, academicYear, ids)
				?? throw new BusinessException("PAYROLL_SUBMISSION_REVIEW_REQUIRED");
			var staffClaims = (await ReadClaimsAsync(period, ids)).ToDictionary(x => x.EmployeeId);
			if (staffClaims.Values.Any(x => x.SubmissionId.HasValue))
				throw new BusinessException("PAYROLL_EMPLOYEE_ALREADY_SUBMITTED");

			var batch = new PayrollSubmission(GuidGenerator.Create(), access.TenantId, academicYear, period, command.PreviewRunId,
				revision, actor, approver.Value, command.SelfFinalize, setting.Version, command.PreviousSubmissionId,
				ready.Sum(x => x.Snapshot.NetPay), ready.Count, Clock.Now);
			await submissions.InsertAsync(batch);
			foreach (var row in ready)
			{
				var id = row.Preview.EmployeeId;
				await submissionRows.InsertAsync(new PayrollSubmissionEmployee(GuidGenerator.Create(), access.TenantId,
					batch.Id, id, row.Preview.Id, evidence.Seal(row), row.Snapshot.NetPay));
				if (staffClaims.TryGetValue(id, out var existing))
				{
					existing.Claim(batch.Id);
					await claims.UpdateAsync(existing);
				}
				else
				{
					var claim = new PayrollEmployeeClaim(GuidGenerator.Create(), access.TenantId, period, id);
					claim.Claim(batch.Id);
					await claims.InsertAsync(claim);
				}
			}
			var roles = new SubmissionRoles(true, command.SelfFinalize, false);
			return await RecordAsync(batch, setting, roles, "submit", command.CommandKey, fingerprint, null, false);
		});
	}

	internal async Task<PayrollSubmissionDto> ResolveAsync(Guid id, string action, ResolvePayrollSubmissionCommand command)
	{
		if (action is not ("finalize" or "withdraw" or "return" or "return-stale") || command.CommandKey == Guid.Empty)
			throw new BusinessException("PAYROLL_SUBMISSION_COMMAND_INVALID");
		var month = (await GetBatchAsync(id)).Month;
		return await UnderFenceAsync(month, async revision =>
		{
			var batch = await GetBatchAsync(id);
			var rows = await ReadRowsAsync(batch);
			var setting = await access.ReadSettingsAsync();
			var roles = await ReadRolesAsync(batch, setting, rows);
			var permitted = action is "withdraw" or "return-stale" ? roles.Submitter || roles.Manager : roles.Approver;
			if (!permitted) throw new AbpAuthorizationException();
			var reason = command.Reason?.Trim();
			var fingerprint = PayrollSubmissionEvidence.Fingerprint(new { id, command.ExpectedVersion, reason });
			var replay = await ReplayAsync(action, command.CommandKey, fingerprint);
			if (replay != null) return replay;
			if (batch.Version != command.ExpectedVersion || batch.State != PayrollSubmission.Pending)
				throw new BusinessException("PAYROLL_SUBMISSION_VERSION_CONFLICT");
			if ((action is "withdraw" or "return") && string.IsNullOrEmpty(reason))
				throw new BusinessException("PAYROLL_CHANGE_REASON_REQUIRED");
			if (action == "finalize" && setting?.Mode != PayrollApprovalSettings.Active)
				throw new BusinessException("PAYROLL_WORKFLOW_NOT_ACTIVE");
			var ids = rows.Select(x => x.EmployeeId).ToList();
			var activeClaims = await ReadClaimsAsync(month, ids);
			if (activeClaims.Count != rows.Count || activeClaims.Any(x => x.SubmissionId != batch.Id))
				throw new BusinessException("PAYROLL_SUBMISSION_CLAIM_MISMATCH");

			var verified = action is "finalize" or "return-stale" ? await ReadCurrentAsync(batch, rows, revision) : null;
			if (action == "return-stale" && verified != null)
				throw new BusinessException("PAYROLL_SUBMISSION_IS_CURRENT");
			var stale = verified == null && (action is "finalize" or "return-stale");
			if (action == "finalize" && verified != null)
			{
				try { await IssueAsync(batch, verified); }
				catch (Exception failure) when (failure is not OperationCanceledException)
				{
					throw new BusinessException("PAYROLL_BATCH_ISSUANCE_FAILED", innerException: failure);
				}
			}
			if (stale) await ClearStaleReviewsAsync(batch, ids);
			var nextState = action == "withdraw" ? PayrollSubmission.Withdrawn
				: action == "finalize" && !stale ? PayrollSubmission.Finalized : PayrollSubmission.Returned;
			batch.Resolve(command.ExpectedVersion, nextState, Clock.Now, stale ? "EvidenceChanged" : action);
			await submissions.UpdateAsync(batch);
			foreach (var claim in activeClaims)
			{
				claim.Release(batch.Id);
				await claims.UpdateAsync(claim);
			}
			return await RecordAsync(batch, setting, roles, action, command.CommandKey, fingerprint, reason, false);
		});
	}

	private async Task IssueAsync(PayrollSubmission batch, List<PayrollVerifiedRow> verified)
	{
		var selected = verified.Select(x => new PayrollTargetEmployee(x.Preview.EmployeeId, x.Preview.EmployeeCode, x.Preview.EmployeeName)).ToList();
		await issuance.EnsureSchoolConfiguredAsync();
		var result = await processor.ProcessAsync(selected, verified.ToDictionary(x => x.Preview.EmployeeId, x => x.Snapshot),
			batch.Month, requireAll: true);
		var ids = selected.Select(x => x.Id).ToList();
		var issued = (await salarySlips.GetByMonthAsync(batch.Month))
			.Where(x => ids.Contains(x.EmployeeId) && x.Status == SalarySlipStatus.Finalized).ToList();
		if (result.GeneratedCount != batch.StaffCount || issued.Count != batch.StaffCount ||
			issued.Sum(x => x.NetSalary) != batch.NetPay)
			throw new BusinessException("PAYROLL_SUBMISSION_ISSUANCE_MISMATCH");
		await continuity.InvalidateAsync(batch.Month, ids);
	}

	private async Task ClearStaleReviewsAsync(PayrollSubmission batch, IReadOnlyCollection<Guid> ids)
	{
		var stale = await AsyncExecuter.ToListAsync((await reviews.GetQueryableAsync())
			.Where(x => x.PreviewRunId == batch.SourceRunId && ids.Contains(x.EmployeeId)));
		var current = (await AsyncExecuter.ToListAsync((await snapshots.GetQueryableAsync())
			.Where(x => x.MonthYear == batch.Month && ids.Contains(x.EmployeeId) && !x.IsLocked))).ToDictionary(x => x.EmployeeId);
		foreach (var mark in stale)
		{
			if (current.TryGetValue(mark.EmployeeId, out var snapshot) && snapshot.ReviewedAt == mark.ReviewedAt &&
				snapshot.ReviewedByUserId == mark.ReviewedByUserId)
			{
				snapshot.MarkPending();
				await snapshots.UpdateAsync(snapshot);
			}
			await reviews.DeleteAsync(mark);
		}
	}

	private async Task<T> UnderFenceAsync<T>(string period, Func<Guid, Task<T>> action) =>
		await monthLocks.WithMonthLockAsync(period, async checkLease =>
		{
			using var unit = UnitOfWorkManager.Begin(new AbpUnitOfWorkOptions
			{ IsTransactional = true, IsolationLevel = IsolationLevel.RepeatableRead }, requiresNew: true);
			var revision = await revisions.LockAsync();
			var result = await action(revision);
			await checkLease();
			await unit.CompleteAsync();
			return result;
		});

	private async Task<PayrollSubmissionDto?> ReplayAsync(string command, Guid key, string fingerprint)
	{
		var receipt = await receipts.FindAsync(x => x.ActorId == access.ActorId && x.Command == command && x.Key == key);
		if (receipt == null) return null;
		if (receipt.Fingerprint != fingerprint) throw new BusinessException("PAYROLL_COMMAND_KEY_CONFLICT");
		var batch = await GetBatchAsync(receipt.ResultId);
		var roles = await ReadRolesAsync(batch, await access.ReadSettingsAsync(), await ReadRowsAsync(batch));
		if (!roles.CanRead) throw new AbpAuthorizationException();
		return JsonSerializer.Deserialize<PayrollSubmissionDto>(receipt.ResultJson);
	}

	private async Task<PayrollSubmissionDto> RecordAsync(PayrollSubmission batch, PayrollApprovalSettings? setting, SubmissionRoles roles,
		string command, Guid key, string fingerprint, string? reason, bool stale)
	{
		var dto = ToDto(batch, roles, setting, stale);
		await receipts.InsertAsync(new PayrollCommandReceipt(GuidGenerator.Create(), access.TenantId, access.ActorId,
			command, key, fingerprint, batch.Id, JsonSerializer.Serialize(dto)));
		await audit.WriteAsync(batch.AcademicYearId, "payroll_submission", batch.Id, batch.Month, command,
			$"Payroll batch {batch.State.ToLowerInvariant()}.", reason,
			context: new { batch.Version, batch.StaffCount, batch.ApproverId, SettingsVersion = setting?.Version, batch.ReasonCode });
		return dto;
	}

	private async Task<PayrollSubmission> GetBatchAsync(Guid id) =>
		await submissions.FindAsync(x => x.Id == id && x.TenantId == access.TenantId)
			?? throw new BusinessException("PAYROLL_SUBMISSION_NOT_FOUND");

	private async Task<List<PayrollSubmissionEmployee>> ReadRowsAsync(PayrollSubmission batch) =>
		await AsyncExecuter.ToListAsync((await submissionRows.GetQueryableAsync())
			.Where(x => x.SubmissionId == batch.Id).OrderBy(x => x.EmployeeId));

	private async Task<List<PayrollEmployeeClaim>> ReadClaimsAsync(string period, IReadOnlyCollection<Guid> ids) =>
		await AsyncExecuter.ToListAsync((await claims.GetQueryableAsync()).Where(x => x.Month == period && ids.Contains(x.EmployeeId)));

	private async Task<List<PayrollVerifiedRow>?> ReadCurrentAsync(PayrollSubmission batch, List<PayrollSubmissionEmployee> rows, Guid revision)
	{
		if (batch.InputRevision != revision || rows.Count != batch.StaffCount) return null;
		var verified = await evidence.ReadCurrentAsync(access.TenantId, batch.SourceRunId, revision, batch.Month, batch.AcademicYearId,
			rows.Select(x => x.EmployeeId).ToList());
		return verified != null && rows.Zip(verified).All(pair => evidence.Matches(pair.First, pair.Second)) ? verified : null;
	}

	private async Task<SubmissionRoles> ReadRolesAsync(PayrollSubmission batch, PayrollApprovalSettings? setting, List<PayrollSubmissionEmployee> rows)
	{
		var actor = access.ActorId;
		if (rows.Count != batch.StaffCount || !await access.CoversAsync(actor, rows.Select(x => x.EmployeeId).ToList()))
			return new(false, false, false);
		var submitter = actor == batch.SubmitterId && await access.CanAsync(actor, Permission.View, Permission.Submit);
		var approver = actor == batch.ApproverId && PayrollApprovalAccess.MatchesRoute(batch, setting) &&
			await access.CanApproveAsync(actor, batch.SelfFinalization);
		return new(submitter, approver, await access.CanManageAsync());
	}

	private static PayrollSubmissionDto ToDto(PayrollSubmission batch, SubmissionRoles roles, PayrollApprovalSettings? setting, bool stale)
	{
		var actions = new List<string>();
		if (batch.State == PayrollSubmission.Pending)
		{
			if (roles.Submitter || roles.Manager)
			{
				actions.Add("withdraw");
				if (stale) actions.Add("return-stale");
			}
			if (roles.Approver)
			{
				actions.Add("return");
				if (setting?.Mode == PayrollApprovalSettings.Active) actions.Add("finalize");
			}
		}
		return new(batch.Id, batch.Month, batch.State, batch.Version, batch.StaffCount, batch.NetPay, batch.SubmitterId,
			batch.ApproverId, batch.SelfFinalization, batch.SubmittedAt, batch.ResolvedAt, batch.ReasonCode, stale, actions.ToArray());
	}

	private sealed record SubmissionRoles(bool Submitter, bool Approver, bool Manager)
	{
		internal bool CanRead => Submitter || Approver || Manager;
	}
}
