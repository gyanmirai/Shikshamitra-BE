using Microsoft.Extensions.Logging;
using ShikshaMitra.HR.Payroll.SalarySlips;
using ShikshaMitra.HR.Payroll.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

public class PayrollLockBatchProcessor(
	ISalarySlipRepository salarySlipRepository,
	IRepository<PayrollRunItemSnapshot, Guid> payrollRunItemSnapshotRepository,
	SalarySlipManager salarySlipManager,
	IUnitOfWorkManager unitOfWorkManager,
	ILogger<PayrollLockBatchProcessor> logger)
	: ITransientDependency
{
	private const int DefaultBatchSize = 50;
	private const int MaxBatchSize = 100;

	internal async Task<PayrollLockBatchResult> ProcessAsync(
		IReadOnlyList<PayrollTargetEmployee> employees,
		IReadOnlyDictionary<Guid, PayrollRunItemSnapshot> snapshotByEmployee,
		string month)
	{
		logger.LogInformation(
			"Generating salary slips from reviewed payroll snapshots for {ProcessCount} employees",
			employees.Count);

		var result = new PayrollLockBatchResult();
		var batchSize = Math.Min(DefaultBatchSize, MaxBatchSize);

		for (int i = 0; i < employees.Count; i += batchSize)
		{
			var batch = employees.Skip(i).Take(batchSize).ToList();
			var batchNum = (i / batchSize) + 1;
			var totalBatches = (int)Math.Ceiling((double)employees.Count / batchSize);

			logger.LogInformation(
				"Processing batch {BatchNum}/{TotalBatches} with {Count} employees",
				batchNum, totalBatches, batch.Count);

			foreach (var employee in batch)
			{
				await ProcessEmployeeAsync(employee, snapshotByEmployee[employee.Id], result);
			}

			await SaveBatchAsync(batchNum, totalBatches, month, result);
		}

		return result;
	}

	private async Task ProcessEmployeeAsync(
		PayrollTargetEmployee employee,
		PayrollRunItemSnapshot snapshot,
		PayrollLockBatchResult result)
	{
		try
		{
			logger.LogDebug(
				"Processing employee {EmployeeCode} ({EmployeeName})",
				employee.EmployeeCode, employee.EmployeeName);

			var earningLineItems = PayrollSnapshotComponentParser.BuildSalarySlipLineItems(snapshot.EarningComponentsJson);
			var deductionLineItems = PayrollSnapshotComponentParser.BuildSalarySlipLineItems(snapshot.DeductionComponentsJson);
			PayrollSnapshotTotalGuard.EnsureMatchesLineItems(snapshot, earningLineItems, deductionLineItems);
			var dayCalculationMode = PayrollRunRules.ParseDayCalculationMode(snapshot.DayCalculationMode);

			var salarySlip = await salarySlipManager.GenerateOrGetSalarySlipFromSnapshotAsync(
				snapshot,
				earningLineItems,
				deductionLineItems,
				dayCalculationMode);

			if (salarySlip.Status == SalarySlipStatus.Finalized)
			{
				logger.LogDebug(
					"Salary slip for employee {EmployeeCode} is already finalized, skipping",
					employee.EmployeeCode);
				result.RecordSkipped();
				snapshot.MarkLocked();
				await payrollRunItemSnapshotRepository.UpdateAsync(snapshot, autoSave: false);
				return;
			}

			salarySlip.FinalizeSlip();
			await salarySlipRepository.UpdateAsync(salarySlip, autoSave: false);
			result.RecordGenerated();
			snapshot.MarkLocked();
			await payrollRunItemSnapshotRepository.UpdateAsync(snapshot, autoSave: false);
		}
		catch (BusinessException bex)
		{
			logger.LogWarning(bex,
				"Business rule violation generating salary slip for employee {EmployeeCode} ({EmployeeName}): {ErrorCode}",
				employee.EmployeeCode, employee.EmployeeName, bex.Code);

			result.RecordFailure(employee.EmployeeCode, employee.EmployeeName, bex.Message ?? bex.Code ?? "Unknown error");
		}
		catch (Exception ex)
		{
			logger.LogError(ex,
				"Unexpected error generating salary slip for employee {EmployeeCode} ({EmployeeName})",
				employee.EmployeeCode, employee.EmployeeName);

			result.RecordFailure(employee.EmployeeCode, employee.EmployeeName, ex.Message);
		}
	}

	private async Task SaveBatchAsync(
		int batchNum,
		int totalBatches,
		string month,
		PayrollLockBatchResult result)
	{
		try
		{
			await unitOfWorkManager.Current!.SaveChangesAsync();
			logger.LogInformation(
				"Batch {BatchNum}/{TotalBatches} saved successfully. Generated: {Generated}, Skipped: {Skipped}, Failed: {Failed}",
				batchNum, totalBatches, result.GeneratedCount, result.SkippedCount, result.FailedCount);
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Error saving batch {BatchNum}. Rolling back batch changes.", batchNum);
			throw new BusinessException(HRErrorCodes.PayrollBatchSaveFailed)
				.WithData("batchNumber", batchNum)
				.WithData("totalBatches", totalBatches)
				.WithData("month", month);
		}
	}
}
