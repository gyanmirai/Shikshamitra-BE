using Microsoft.Extensions.Logging;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Audit;
using ShikshaMitra.HR.Payroll.SalarySlips;
using ShikshaMitra.HR.Payroll.Services;
using ShikshaMitra.HR.Payroll.Statutory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Guids;
using Volo.Abp.Uow;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

public class PayrollLockWorkflow(
	PayrollPreviewReviewGuard previewGuard,
	ISalarySlipRepository salarySlipRepository,
	PayrollCalendarResolver payrollCalendarResolver,
	HrAuditWriter auditWriter,
	PayrollIneligibleRowCleaner ineligibleRowCleaner,
	PayrollScopedEmployeeResolver scopedEmployeeResolver,
	PayrollRunSnapshotStore snapshotStore,
	IUnitOfWorkManager uows,
	PayrollSnapshotLockProvider snapshotLockProvider,
	PayrollLockBatchProcessor batchProcessor,
	PayslipIssuanceService issuance,
	EsiContinuityInvalidator continuityInvalidator,
	PayrollAcademicYearScopeResolver academicYearScopeResolver,
	IGuidGenerator guidGenerator,
	ILogger<PayrollLockWorkflow> logger)
	: ITransientDependency
{
	public async Task<PayrollLockResponse> LockAsync(RunPayrollRequest request)
	{
		var (year, month, internalMonthYear) = PayrollRunRules.ParseMonth(request.Month);
		var academicYearId = await academicYearScopeResolver.ResolveAsync(request.AcademicYearId, year, month);

		return await snapshotLockProvider.WithMonthLockAsync(
			internalMonthYear,
			async checkLease =>
			{
				using var transaction = uows.Begin(requiresNew: true, isTransactional: true);
				var response = await LockCoreAsync(request, year, month, internalMonthYear, academicYearId);
				await checkLease();
				await transaction.CompleteAsync();
				return response;
			});
	}

	private async Task<PayrollLockResponse> LockCoreAsync(
		RunPayrollRequest request,
		int year,
		int month,
		string internalMonthYear,
		Guid? academicYearId)
	{
		await previewGuard.EnsureCurrentAsync(request.PreviewRunId, request.Month, academicYearId, request.EmployeeIds);
		await previewGuard.EnsureReviewedAsync(request.PreviewRunId, request.EmployeeIds!);
		using var scope = logger.BeginScope(new Dictionary<string, object>
		{
			["Month"] = request.Month,
			["Year"] = year,
			["MonthNum"] = month,
			["OperationType"] = "PayrollLock"
		});

		logger.LogInformation("Starting payroll lock operation for {Month}", request.Month);

		var allScopedEmployees = await scopedEmployeeResolver.GetScopedEmployeesAsync(includeUnknownStageEmployees: false);
		var eligibleEmployees = await scopedEmployeeResolver.GetScopedPayrollEmployeesAsync(year, month, includeUnknownStageEmployees: false);
		await ineligibleRowCleaner.CleanupAsync(internalMonthYear, allScopedEmployees, eligibleEmployees);
		var scopedEmployees = eligibleEmployees
			.Select(e => new PayrollTargetEmployee(e.Id, e.EmployeeCode, e.EmployeeName))
			.ToList();

		if (scopedEmployees.Count == 0)
		{
			logger.LogWarning("No active employees found for payroll generation in {Month}", request.Month);
			throw new BusinessException(HRErrorCodes.PayrollNoEmployees)
				.WithData("month", request.Month);
		}

		var targetEmployees = PayrollTargetEmployeeResolver.ResolvePayrollLockTargetEmployees(
			allScopedEmployees.Select(e => new PayrollTargetEmployee(e.Id, e.EmployeeCode, e.EmployeeName)).ToList(),
			scopedEmployees,
			request.EmployeeIds,
			request.Month);
		var lockCalendar = await payrollCalendarResolver.ResolveAsync(year, month, academicYearId);

		var existingSlips = await salarySlipRepository.GetByMonthAsync(internalMonthYear);
		var targetEmployeeIds = targetEmployees.Select(e => e.Id).ToList();
		PayrollLockPreconditionGuard.EnsureNoFinalizedSlips(targetEmployeeIds, existingSlips, request.Month);

		var employees = targetEmployees;

		var snapshotByEmployee = (await snapshotStore.GetByMonthAsync(internalMonthYear))
			.ToDictionary(x => x.EmployeeId, x => x);
		PayrollLockPreconditionGuard.EnsureReviewed(targetEmployeeIds, snapshotByEmployee, request.Month);
		PayrollLockPreconditionGuard.EnsureNoBlockingSetupIssues(targetEmployeeIds, snapshotByEmployee, request.Month);

		logger.LogInformation(
			"Generating salary slips from reviewed payroll snapshots for {ProcessCount} employees",
			employees.Count);

		await issuance.EnsureSchoolConfiguredAsync();
		var result = await batchProcessor.ProcessAsync(employees, snapshotByEmployee, request.Month);
		await continuityInvalidator.InvalidateAsync(internalMonthYear, targetEmployeeIds);

		if (result.GeneratedCount == 0 && result.FailedCount == employees.Count)
		{
			logger.LogError(
				"Payroll generation failed for all {Count} employees in {Month}",
				employees.Count, request.Month);
			result.ThrowIfCompleteFailure(employees.Count, request.Month);
		}

		logger.LogInformation(
			"Payroll lock completed for {Month}. Generated: {Generated}, Skipped: {Skipped}, Failed: {Failed}, Total: {Total}",
			request.Month, result.GeneratedCount, result.SkippedCount, result.FailedCount, result.TotalProcessed);

		var response = new PayrollLockResponse
		{
			Message = result.BuildResponseMessage(),
			Month = request.Month,
			WasAlreadyLocked = false,
			EmployeesProcessed = result.GeneratedCount,
			EmployeesSkipped = result.SkippedCount,
			EmployeesFailed = result.FailedCount,
			Errors = result.BuildResponseErrors()
		};
		await auditWriter.WriteAsync(
			lockCalendar.AcademicYearId,
			"payroll_run",
			guidGenerator.Create(),
			request.Month,
			"locked",
			$"Locked payroll for {request.Month}.",
			context: new
			{
				RequestedMonth = request.Month,
				Year = year,
				MonthNumber = month,
				employeeCount = employees.Count,
				generatedCount = result.GeneratedCount,
				skippedCount = result.SkippedCount,
				failedCount = result.FailedCount,
				hasPolicyMetadataSnapshots = snapshotByEmployee.Values.Any(x => x.PayrollPolicyId.HasValue || x.AttendancePolicyId.HasValue || x.WorkCalendarPolicyId.HasValue)
			});
		return response;
	}
}
