using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.HR.Payroll.Statutory;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

[RemoteService(false)]
public class PayslipCorrectionAppService(IRepository<PayslipCorrection, Guid> corrections,
	IRepository<PayslipDocument, Guid> documents, IRepository<WorkCalendarPolicy, Guid> calendars,
	IRepository<EmployeeAttendance, Guid> attendanceRecords,
	IRepository<PayrollRunItemSnapshot, Guid> snapshots, ISalarySlipRepository slips,
	SalarySlipAccessService access, IPayrollInputRevisionStore fence, PayslipIssuanceService issuance,
	PayrollSnapshotRefresher calculator, SalarySlipSnapshotWriter writer,
	EsiContinuityInvalidator continuity, IRepository<PayrollPreviewRun, Guid> previewRuns,
	IRepository<PayrollPreviewEmployee, Guid> previewEmployees, IRepository<PayrollRunReview, Guid> payrollReviews,
	PayrollScopedEmployeeResolver employeeScope) : ApplicationService, IPayslipCorrectionAppService
{
	private async Task EnsureReadAsync()
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_GenerateSalarySlip.View);
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_RunPayroll.View);
	}
	private async Task EnsureEditAsync()
	{
		await EnsureReadAsync();
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_GenerateSalarySlip.Add);
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_RunPayroll.Adjustments);
	}
	public async Task<List<PayslipCorrectionDto>> GetAsync(Guid documentId)
	{
		await EnsureReadAsync();
		var document = await documents.GetAsync(documentId);
		await access.EnsureEmployeeScopeAllowedAsync(document.EmployeeId);
		return (await corrections.GetListAsync(x => x.SalarySlipId == document.SalarySlipId))
			.OrderByDescending(x => x.CreationTime).Select(ToDto).ToList();
	}
	[UnitOfWork(true)]
	public virtual async Task<PayslipCorrectionDto> StartAsync(StartPayslipCorrectionDto input)
	{
		await EnsureEditAsync();
		await fence.LockAsync();
		var document = await documents.GetAsync(input.DocumentId);
		await access.EnsureEmployeeScopeAllowedAsync(document.EmployeeId);
		var slip = await slips.GetAsync(document.SalarySlipId);
		if (slip.Status is not (SalarySlipStatus.Finalized or SalarySlipStatus.Paid))
			throw new BusinessException(HRErrorCodes.PayslipConflict);
		var repeated = await corrections.FindAsync(x => x.SalarySlipId == document.SalarySlipId && x.RequestId == input.IdempotencyKey);
		if (repeated != null)
		{
			if (repeated.SourceDocumentId != document.Id || repeated.Reason != input.Reason?.Trim())
				throw new BusinessException(HRErrorCodes.PayslipConflict);
			return ToDto(repeated);
		}
		if ((await issuance.GetCurrentAsync(document.SalarySlipId)).Id != document.Id ||
			await corrections.AnyAsync(x => x.SalarySlipId == document.SalarySlipId &&
				(x.Status == "DRAFT" || x.Status == "CALCULATED" || x.Status == "REVIEWED")))
			throw new BusinessException(HRErrorCodes.PayslipConflict);
		var source = await snapshots.GetAsync(x => x.EmployeeId == document.EmployeeId && x.MonthYear == document.MonthYear);
		if (!source.WorkCalendarPolicyId.HasValue) throw new BusinessException(HRErrorCodes.PayslipConflict);
		var calendar = await calendars.GetAsync(source.WorkCalendarPolicyId.Value);
		var previousCorrection = (await corrections.GetListAsync(x => x.SalarySlipId == document.SalarySlipId &&
			x.Status == "APPLIED")).OrderByDescending(x => x.CompletedAt).FirstOrDefault();
		var baseline = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(source))!;
		if (previousCorrection?.SettlementMonth != null)
		{
			var prior = JsonSerializer.Deserialize<RunPayrollItem>(previousCorrection.CandidateJson!)!;
			baseline["NetPay"] = prior.NetPay;
			baseline["TotalDeduction"] = prior.TotalDeduction;
			baseline["EmployerCost"] = prior.EmployerCost;
			baseline["EarningComponentsJson"] = JsonSerializer.Serialize(prior.EarningComponents);
			baseline["DeductionComponentsJson"] = JsonSerializer.Serialize(prior.DeductionComponents);
			baseline["EmployerContributionsJson"] = JsonSerializer.Serialize(prior.EmployerContributions);
		}
		var correction = new PayslipCorrection(GuidGenerator.Create(), document, calendar.AcademicYearId,
			input.Reason, input.IdempotencyKey, baseline.ToJsonString());
		var date = DateOnly.ParseExact(document.MonthYear + "-01", "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
		if (previousCorrection != null)
			correction.InitializeAttendance(previousCorrection.AttendanceJson);
		else
		{
			var attendance = await attendanceRecords.GetListAsync(x => x.EmployeeId == document.EmployeeId &&
				x.AttendanceDate >= date && x.AttendanceDate < date.AddMonths(1));
			correction.InitializeAttendance(JsonSerializer.Serialize(attendance.OrderBy(x => x.AttendanceDate)
				.Select(x => new PayslipCorrectionAttendanceDto
				{
					Date = x.AttendanceDate,
					Status = x.StatusType,
					TimeIn = x.TimeIn,
					TimeOut = x.TimeOut
				})));
		}
		await corrections.InsertAsync(correction, autoSave: true);
		return ToDto(correction);
	}
	[UnitOfWork(true)]
	public virtual async Task<PayslipCorrectionDto> CalculateAsync(Guid id, CalculatePayslipCorrectionDto input)
	{
		await EnsureEditAsync();
		var revision = await fence.LockAsync();
		var correction = await LoadForChangeAsync(id, input.ConcurrencyStamp);
		if (input.Attendance == null || input.Attendance.Count > 31 || input.Attendance.Any(x => x == null))
			throw new BusinessException(HRErrorCodes.PayslipConflict);
		var attendance = input.Attendance.Select(x => new EmployeeAttendance
		{
			EmployeeId = correction.EmployeeId,
			TenantId = correction.TenantId,
			AttendanceDate = x.Date,
			StatusType = x.Status,
			TimeIn = x.TimeIn,
			TimeOut = x.TimeOut
		}).ToList();
		var candidate = await calculator.CalculateCorrectionAsync(correction.EmployeeId, correction.SourceMonth,
			correction.AcademicYearId, attendance);
		correction.Calculate(JsonSerializer.Serialize(input.Attendance), JsonSerializer.Serialize(candidate), revision);
		await corrections.UpdateAsync(correction, autoSave: true);
		return ToDto(correction);
	}
	[UnitOfWork(true)]
	public virtual async Task<PayslipCorrectionDto> ReviewAsync(Guid id, PayslipCorrectionActionDto input)
	{
		await EnsureEditAsync();
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_RunPayroll.Add);
		var revision = await fence.LockAsync();
		var correction = await LoadForChangeAsync(id, input.ConcurrencyStamp);
		var candidate = JsonSerializer.Deserialize<RunPayrollItem>(correction.CandidateJson ?? "null")
			?? throw new BusinessException(HRErrorCodes.PayslipConflict);
		// Coverage evidence can change without bumping the ordinary input revision.
		// Re-run under the tenant fence before accepting a review of saved evidence.
		EnsureSameCalculation(candidate, await RecalculateAsync(correction));
		var snapshot = new PayrollRunItemSnapshot(GuidGenerator.Create(), correction.TenantId,
			correction.SourceMonth, correction.EmployeeId);
		PayrollRunProjection.ApplySnapshotValues(snapshot, candidate, JsonSerializer.Serialize(candidate.IssueCodes),
			JsonSerializer.Serialize(candidate.AttendancePolicyWarnings), JsonSerializer.Serialize(candidate.EarningComponents),
			JsonSerializer.Serialize(candidate.DeductionComponents), JsonSerializer.Serialize(candidate.EmployerContributions),
			JsonSerializer.Serialize(candidate.PayrollAdjustments));
		PayrollLockPreconditionGuard.EnsureNoBlockingSetupIssues([correction.EmployeeId],
			new Dictionary<Guid, PayrollRunItemSnapshot> { [correction.EmployeeId] = snapshot }, correction.SourceMonth);
		correction.Review(revision, CurrentUser.Id ?? Guid.Empty, Clock.Now);
		await corrections.UpdateAsync(correction, autoSave: true);
		return ToDto(correction);
	}
	[UnitOfWork(true)]
	public virtual async Task<PayslipCorrectionDto> CancelAsync(Guid id, PayslipCorrectionActionDto input)
	{
		await EnsureEditAsync();
		await fence.LockAsync();
		var existing = await corrections.GetAsync(id);
		await access.EnsureEmployeeScopeAllowedAsync(existing.EmployeeId);
		if (existing.Status == "CANCELLED") return ToDto(existing);
		var correction = await LoadForChangeAsync(id, input.ConcurrencyStamp);
		correction.Cancel(CurrentUser.Id ?? Guid.Empty, Clock.Now);
		await corrections.UpdateAsync(correction, autoSave: true);
		return ToDto(correction);
	}
	private async Task<PayslipCorrection> LoadForChangeAsync(Guid id, string stamp)
	{
		var correction = await corrections.GetAsync(id);
		await access.EnsureEmployeeScopeAllowedAsync(correction.EmployeeId);
		if (string.IsNullOrWhiteSpace(stamp) || correction.ConcurrencyStamp != stamp || !correction.IsActive ||
			(await issuance.GetCurrentAsync(correction.SalarySlipId)).Id != correction.SourceDocumentId)
			throw new BusinessException(HRErrorCodes.PayslipConflict);
		var slip = await slips.GetAsync(correction.SalarySlipId);
		if (slip.Status is not (SalarySlipStatus.Finalized or SalarySlipStatus.Paid))
			throw new BusinessException(HRErrorCodes.PayslipConflict);
		return correction;
	}

	[UnitOfWork(true)]
	public virtual async Task<PayslipCorrectionDto> ApplyAsync(Guid id, PayslipCorrectionActionDto input)
	{
		await EnsureEditAsync();
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_RunPayroll.Lock);
		var revision = await fence.LockAsync();
		var correction = await corrections.GetAsync(id);
		await access.EnsureEmployeeScopeAllowedAsync(correction.EmployeeId);
		if (correction.Status == "APPLIED") return ToDto(correction);
		correction = await LoadForChangeAsync(id, input.ConcurrencyStamp);
		if (correction.Status != "REVIEWED" || correction.InputRevision != revision)
			throw new BusinessException(HRErrorCodes.PayslipConflict);
		var slip = await slips.GetAsync(correction.SalarySlipId);
		if (slip.Status != SalarySlipStatus.Finalized) throw new BusinessException(HRErrorCodes.PayslipConflict);
		var candidate = await RecalculateAsync(correction);
		EnsureSameCalculation(candidate, JsonSerializer.Deserialize<RunPayrollItem>(correction.CandidateJson!)!);
		var snapshot = await snapshots.GetAsync(x => x.EmployeeId == correction.EmployeeId && x.MonthYear == correction.SourceMonth);
		var hadEsiEvidence = snapshot.EmployeeStateInsuranceRuleId.HasValue || slip.EmployeeStateInsuranceRuleId.HasValue;
		PayrollRunProjection.ApplySnapshotValues(snapshot, candidate, JsonSerializer.Serialize(candidate.IssueCodes),
			JsonSerializer.Serialize(candidate.AttendancePolicyWarnings), JsonSerializer.Serialize(candidate.EarningComponents),
			JsonSerializer.Serialize(candidate.DeductionComponents), JsonSerializer.Serialize(candidate.EmployerContributions),
			JsonSerializer.Serialize(candidate.PayrollAdjustments));
		PayrollLockPreconditionGuard.EnsureNoBlockingSetupIssues([correction.EmployeeId],
			new Dictionary<Guid, PayrollRunItemSnapshot> { [correction.EmployeeId] = snapshot }, correction.SourceMonth);
		var earnings = PayrollSnapshotComponentParser.BuildSalarySlipLineItems(snapshot.EarningComponentsJson);
		var deductions = PayrollSnapshotComponentParser.BuildSalarySlipLineItems(snapshot.DeductionComponentsJson);
		PayrollSnapshotTotalGuard.EnsureMatchesLineItems(snapshot, earnings, deductions);
		snapshot.IsLocked = true;
		snapshot.MarkReviewed(correction.ReviewedBy!.Value, correction.ReviewedAt!.Value);
		await writer.ApplyUnpaidCorrectionAsync(slip, snapshot, earnings, deductions,
			PayrollSnapshotComponentParser.BuildSalarySlipLineItems(snapshot.EmployerContributionsJson),
			SalarySlipSnapshotMatcher.ParseDayCalculationMode(snapshot.DayCalculationMode));
		await snapshots.UpdateAsync(snapshot, autoSave: true);
		var source = await documents.GetAsync(correction.SourceDocumentId);
		var result = await issuance.IssueCorrectionAsync(slip, source, correction.Id, correction.Reason);
		correction.Complete(revision, CurrentUser.Id ?? Guid.Empty, Clock.Now, result.Id, null);
		await corrections.UpdateAsync(correction, autoSave: true);
		await continuity.InvalidateAsync(correction.SourceMonth, [correction.EmployeeId],
			hadEsiEvidence ? [correction.EmployeeId] : []);
		return ToDto(correction);
	}
	[UnitOfWork(true)]
	public virtual async Task<PayslipCorrectionDto> SettleAsync(Guid id, SettlePayslipCorrectionDto input)
	{
		await EnsureEditAsync();
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_RunPayroll.Lock);
		var revision = await fence.LockAsync();
		var correction = await corrections.GetAsync(id);
		await access.EnsureEmployeeScopeAllowedAsync(correction.EmployeeId);
		if (correction.Status == "APPLIED")
		{
			if (correction.SettlementMonth != input.TargetMonth) throw new BusinessException(HRErrorCodes.PayslipConflict);
			return ToDto(correction);
		}
		correction = await LoadForChangeAsync(id, input.ConcurrencyStamp);
		if (correction.Status != "REVIEWED" || correction.InputRevision != revision ||
			(await slips.GetAsync(correction.SalarySlipId)).Status != SalarySlipStatus.Paid ||
			!DateTime.TryParseExact(input.TargetMonth, "yyyy-MM", System.Globalization.CultureInfo.InvariantCulture,
				System.Globalization.DateTimeStyles.None, out var targetMonth) ||
			string.CompareOrdinal(input.TargetMonth, correction.SourceMonth) <= 0)
			throw new BusinessException(HRErrorCodes.PayslipConflict);
		var eligible = await employeeScope.ResolveScopedEmployeesByIdsAsync(targetMonth.Year, targetMonth.Month, [correction.EmployeeId], false);
		if (eligible.Count != 1) throw new BusinessException(HRErrorCodes.PayrollPreviewNotFound);
		var run = await previewRuns.GetAsync(input.PreviewRunId);
		if (run.Month != input.TargetMonth || run.Status != "SUCCEEDED" || run.InputRevision != revision)
			throw new BusinessException(HRErrorCodes.PayrollPreviewStale);
		var target = await snapshots.FindAsync(x => x.EmployeeId == correction.EmployeeId && x.MonthYear == input.TargetMonth);
		if (target == null || target.IsLocked || await slips.AnyAsync(x => x.EmployeeId == correction.EmployeeId &&
			x.MonthYear == input.TargetMonth && (x.Status == SalarySlipStatus.Finalized || x.Status == SalarySlipStatus.Paid)))
			throw new BusinessException(HRErrorCodes.PayslipConflict).WithData("reason", "Select a calculated, unlocked later payroll.");
		var row = await previewEmployees.GetAsync(x => x.RunId == run.Id && x.EmployeeId == correction.EmployeeId);
		var targetItem = JsonSerializer.Deserialize<RunPayrollItem>(row.ItemJson)!;
		EnsureSameCalculation(JsonSerializer.Deserialize<RunPayrollItem>(correction.CandidateJson!)!, await RecalculateAsync(correction));
		// Validate the full cash reconciliation and recovery limits before committing the immutable source delta.
		PayslipSettlementProjection.Apply(targetItem, correction);
		correction.Complete(revision, CurrentUser.Id ?? Guid.Empty, Clock.Now, null, input.TargetMonth);
		await corrections.UpdateAsync(correction);
		foreach (var affected in await previewRuns.GetListAsync(x => x.Month == input.TargetMonth &&
			(x.Status == "QUEUED" || x.Status == "RUNNING" || x.Status == "SUCCEEDED")))
		{
			affected.Supersede(Clock.Now);
			await previewRuns.UpdateAsync(affected);
		}
		foreach (var review in await payrollReviews.GetListAsync(x => x.EmployeeId == correction.EmployeeId && x.MonthYear == input.TargetMonth))
			await payrollReviews.DeleteAsync(review);
		await snapshots.DeleteAsync(target);
		await continuity.InvalidateAsync(correction.SourceMonth, [correction.EmployeeId], [correction.EmployeeId]);
		await CurrentUnitOfWork!.SaveChangesAsync();
		return ToDto(correction);
	}
	private async Task<RunPayrollItem> RecalculateAsync(PayslipCorrection correction)
	{
		var attendance = JsonSerializer.Deserialize<List<PayslipCorrectionAttendanceDto>>(correction.AttendanceJson)!;
		return await calculator.CalculateCorrectionAsync(correction.EmployeeId, correction.SourceMonth,
			correction.AcademicYearId, attendance.Select(x => new EmployeeAttendance
			{
				EmployeeId = correction.EmployeeId,
				TenantId = correction.TenantId,
				AttendanceDate = x.Date,
				StatusType = x.Status,
				TimeIn = x.TimeIn,
				TimeOut = x.TimeOut
			}).ToList());
	}
	internal static void EnsureSameCalculation(RunPayrollItem expected, RunPayrollItem actual)
	{
		// jsonb also normalizes negative zero and decimal scale; compare exact values without rounding.
		if (!JsonElement.DeepEquals(JsonSerializer.SerializeToElement(expected), JsonSerializer.SerializeToElement(actual)))
			throw new BusinessException(HRErrorCodes.PayslipConflict);
	}
	private static PayslipCorrectionDto ToDto(PayslipCorrection value)
	{
		using var original = JsonDocument.Parse(value.OriginalSnapshotJson);
		var facts = original.RootElement;
		return new(value.Id, value.SourceDocumentId, value.Reason, value.Status, value.ConcurrencyStamp,
			value.CandidateJson == null ? null : JsonSerializer.Deserialize<RunPayrollItem>(value.CandidateJson),
			value.ReviewedBy, value.ReviewedAt, value.ResultDocumentId, value.SettlementMonth)
		{
			SourceMonth = value.SourceMonth,
			Attendance = JsonSerializer.Deserialize<List<PayslipCorrectionAttendanceDto>>(value.AttendanceJson)!,
			OriginalNetPay = facts.GetProperty("NetPay").GetDecimal(),
			OriginalDeductions = facts.GetProperty("TotalDeduction").GetDecimal(),
			OriginalEarnings = facts.GetProperty("NetPay").GetDecimal() + facts.GetProperty("TotalDeduction").GetDecimal(),
			OriginalEmployerCost = facts.GetProperty("EmployerCost").GetDecimal()
		};
	}
}