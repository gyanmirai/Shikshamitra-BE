using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.Audit;
using ShikshaMitra.HR.Payroll.SalarySlips;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

[RemoteService(false)]
public class PayrollReviewWorkflow(
	PayrollPreviewReviewGuard previewGuard,
	ISalarySlipRepository salarySlipRepository,
	IRepository<PayrollRunReview, Guid> payrollRunReviewRepository,
	IRepository<PayrollRunItemSnapshot, Guid> payrollRunItemSnapshotRepository,
	PayrollAdjustmentReader adjustmentReader,
	PayrollScopedEmployeeResolver scopedEmployeeResolver,
	PayrollRunSnapshotStore snapshotStore,
	PayrollAcademicYearScopeResolver academicYearScopeResolver,
	HrAuditWriter auditWriter,
	IRepository<HrAuditEvent, Guid> auditEvents) : ApplicationService, ITransientDependency
{
	public async Task<PayrollLockResponse> MarkReviewedAsync(PayrollRunReviewRequest request)
	{
		await previewGuard.EnsureCurrentAsync(request.PreviewRunId, request.Month, request.AcademicYearId, request.EmployeeIds);
		var (year, month, internalMonthYear) = PayrollRunRules.ParseMonth(request.Month);
		var academicYearId = await academicYearScopeResolver.ResolveAsync(request.AcademicYearId, year, month);
		var employeeIds = PayrollRunRules.NormalizeReviewEmployeeIds(request.EmployeeIds);
		var actorUserId = CurrentUser.Id ?? throw new BusinessException(HRErrorCodes.AttendanceImportPreviewInvalid);
		const string action = "reviewed";
		var operationKey = GetOperationKey(request, action);

		var targetEmployees = await scopedEmployeeResolver.ResolveScopedTargetEmployeesAsync(year, month, employeeIds);
		if (await FindRecordedOperationAsync(request.PreviewRunId, operationKey) is { } recorded)
			return BuildResponse(request.Month, action, recorded);
		await EnsureReviewCanChangeAsync(internalMonthYear, request.Month, targetEmployees);

		var existingByEmployee = await GetReviewsByEmployeeAsync(internalMonthYear);
		var snapshotByEmployee = (await snapshotStore.GetByMonthAsync(internalMonthYear))
			.ToDictionary(x => x.EmployeeId, x => x);
		var missingSnapshotEmployeeIds = targetEmployees
			.Where(e => !snapshotByEmployee.ContainsKey(e.Id))
			.Select(e => e.Id)
			.ToList();
		if (missingSnapshotEmployeeIds.Count > 0)
		{
			throw new BusinessException(HRErrorCodes.PayrollReviewRequiredBeforeLock)
				.WithData("month", request.Month)
				.WithData("employeeIds", missingSnapshotEmployeeIds);
		}

		var reviewedAt = Clock.Now;
		var updated = 0;

		foreach (var employee in targetEmployees)
		{
			if (existingByEmployee.TryGetValue(employee.Id, out var existingReview) && existingReview.PreviewRunId == request.PreviewRunId)
				continue;
			var evidence = snapshotByEmployee[employee.Id];
			var profile = evidence.StatutoryApplicationJson == null ? null : JsonSerializer.Deserialize<ShikshaMitra.HR.Payroll.Statutory.PayrollStatutoryApplication>(evidence.StatutoryApplicationJson)?.Profile;
			var warnings = JsonSerializer.Deserialize<List<string>>(evidence.IssueCodesJson) ?? [];
			if (existingReview is not null)
			{
				existingReview.Refresh(actorUserId, reviewedAt, request.PreviewRunId);
				existingReview.AcknowledgeWarnings(warnings, profile?.VersionId);
				await payrollRunReviewRepository.UpdateAsync(existingReview, autoSave: false);
			}
			else
			{
				var review = new PayrollRunReview(GuidGenerator.Create(), CurrentTenant.Id, internalMonthYear, employee.Id, actorUserId, reviewedAt, request.PreviewRunId);
				review.AcknowledgeWarnings(warnings, profile?.VersionId);
				await payrollRunReviewRepository.InsertAsync(review, autoSave: false);
			}

			if (snapshotByEmployee.TryGetValue(employee.Id, out var snapshot) && !snapshot.IsLocked)
			{
				snapshot.MarkReviewed(actorUserId, reviewedAt);
				await payrollRunItemSnapshotRepository.UpdateAsync(snapshot, autoSave: false);
			}
			updated++;
		}

		return await SaveReviewAsync(request, academicYearId, $"{year:0000}-{month:00}", action, updated, operationKey);
	}

	public async Task<PayrollLockResponse> UnmarkReviewedAsync(PayrollRunReviewRequest request)
	{
		await previewGuard.EnsureCurrentAsync(request.PreviewRunId, request.Month, request.AcademicYearId, request.EmployeeIds);
		var (year, month, internalMonthYear) = PayrollRunRules.ParseMonth(request.Month);
		var academicYearId = await academicYearScopeResolver.ResolveAsync(request.AcademicYearId, year, month);
		var employeeIds = PayrollRunRules.NormalizeReviewEmployeeIds(request.EmployeeIds);
		const string action = "review_removed";
		var operationKey = GetOperationKey(request, action);
		var targetEmployees = await scopedEmployeeResolver.ResolveScopedTargetEmployeesAsync(year, month, employeeIds);
		if (await FindRecordedOperationAsync(request.PreviewRunId, operationKey) is { } recorded)
			return BuildResponse(request.Month, action, recorded);
		await EnsureReviewCanChangeAsync(internalMonthYear, request.Month, targetEmployees);

		var existingByEmployee = await GetReviewsByEmployeeAsync(internalMonthYear);
		var snapshotByEmployee = (await snapshotStore.GetByMonthAsync(internalMonthYear))
			.ToDictionary(x => x.EmployeeId, x => x);
		var adjustmentEmployeeIds = (await adjustmentReader.GetByEmployeeAsync(
				internalMonthYear,
				targetEmployees.Select(x => x.Id).ToList()))
			.Where(x => x.Value.Count > 0)
			.Select(x => x.Key)
			.ToList();
		if (adjustmentEmployeeIds.Count > 0)
		{
			throw new BusinessException(HRErrorCodes.PayrollReviewContainsAdjustments)
				.WithData("month", request.Month)
				.WithData("employeeIds", adjustmentEmployeeIds);
		}

		var removed = 0;
		foreach (var employee in targetEmployees)
		{
			if (!existingByEmployee.TryGetValue(employee.Id, out var existingReview) || existingReview.PreviewRunId != request.PreviewRunId)
			{
				continue;
			}

			await payrollRunReviewRepository.DeleteAsync(existingReview, autoSave: false);
			if (snapshotByEmployee.TryGetValue(employee.Id, out var snapshot))
			{
				snapshot.MarkPending();
				await payrollRunItemSnapshotRepository.UpdateAsync(snapshot, autoSave: false);
			}
			removed++;
		}

		return await SaveReviewAsync(request, academicYearId, $"{year:0000}-{month:00}", action, removed, operationKey);
	}

	private string? GetOperationKey(PayrollRunReviewRequest request, string action)
	{
		if (request.OperationId is null)
			return null;
		var employeeIds = string.Join(",", request.EmployeeIds.Distinct().Order());
		var fingerprint = $"{CurrentUser.Id}:{request.OperationId}:{action}:{request.PreviewRunId}:{employeeIds}";
		return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint)));
	}

	private async Task<int?> FindRecordedOperationAsync(Guid previewRunId, string? operationKey)
	{
		if (operationKey is null)
			return null;
		var recorded = await auditEvents.FindAsync(x => x.EntityType == "payroll_run"
			&& x.TenantId == CurrentTenant.Id && x.EntityId == previewRunId && x.CorrelationId == operationKey);
		if (recorded?.ContextJson is null)
			return null;
		using var context = JsonDocument.Parse(recorded.ContextJson);
		return context.RootElement.GetProperty("employeeCount").GetInt32();
	}

	private async Task<PayrollLockResponse> SaveReviewAsync(
		PayrollRunReviewRequest request, Guid? academicYearId, string month, string action, int count, string? operationKey)
	{
		var response = BuildResponse(month, action, count);
		if (count > 0 || operationKey is not null)
		{
			// Store idempotency receipts even for no-ops, without displaying them as activity.
			var recordedAction = count > 0 ? action : HrAuditTrailQuery.PayrollReviewReceiptAction;
			await auditWriter.WriteAsync(academicYearId, "payroll_run", request.PreviewRunId, month, recordedAction, response.Message,
				context: new { request.PreviewRunId, EmployeeCount = count }, correlationId: operationKey);
		}
		await UnitOfWorkManager.Current!.SaveChangesAsync();
		return response;
	}

	private static PayrollLockResponse BuildResponse(string month, string action, int count) => new()
	{
		Message = action == "reviewed"
			? $"Marked {count} payroll row(s) as reviewed."
			: $"Removed review from {count} payroll row(s).",
		Month = month,
		EmployeesProcessed = count
	};

	internal async Task EnsureReviewCanChangeAsync(
		string internalMonthYear,
		string requestMonth,
		List<PayrollTargetEmployee> targetEmployees)
	{
		var existingSlips = await salarySlipRepository.GetByMonthAsync(internalMonthYear);
		var lockedIds = existingSlips
			.Where(x => x.Status is SalarySlipStatus.Finalized or SalarySlipStatus.Paid)
			.Select(x => x.EmployeeId)
			.ToHashSet();
		var lockedRequestedIds = targetEmployees
			.Where(e => lockedIds.Contains(e.Id))
			.Select(e => e.Id)
			.ToList();
		if (lockedRequestedIds.Count > 0)
		{
			throw new BusinessException(HRErrorCodes.PayrollReviewCannotChangeLocked)
				.WithData("month", requestMonth)
				.WithData("employeeIds", lockedRequestedIds);
		}
	}

	private async Task<Dictionary<Guid, PayrollRunReview>> GetReviewsByEmployeeAsync(string monthYear)
	{
		var query = await payrollRunReviewRepository.GetQueryableAsync();

		return (await AsyncExecuter.ToListAsync(query.Where(x => x.MonthYear == monthYear)))
			.GroupBy(x => x.EmployeeId)
			.ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.ReviewedAt).First());
	}
}
