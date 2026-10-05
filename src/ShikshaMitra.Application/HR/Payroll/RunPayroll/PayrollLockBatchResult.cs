using System.Collections.Generic;
using System.Linq;
using Volo.Abp;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

internal sealed class PayrollLockBatchResult
{
	private readonly List<(string EmployeeCode, string EmployeeName, string Error)> errors = [];

	public int GeneratedCount { get; private set; }
	public int SkippedCount { get; private set; }
	public int FailedCount { get; private set; }
	public int TotalProcessed => GeneratedCount + SkippedCount + FailedCount;
	public IReadOnlyList<(string EmployeeCode, string EmployeeName, string Error)> Errors => errors;

	public void RecordGenerated() => GeneratedCount++;

	public void RecordSkipped() => SkippedCount++;

	public void RecordFailure(string? employeeCode, string? employeeName, string error)
	{
		FailedCount++;
		errors.Add((employeeCode ?? "UNKNOWN", employeeName ?? "Unknown", error));
	}

	public void ThrowIfCompleteFailure(int employeeCount, string month)
	{
		if (GeneratedCount != 0 || FailedCount != employeeCount)
		{
			return;
		}

		var firstError = errors.FirstOrDefault();
		throw new BusinessException(HRErrorCodes.PayrollGenerationCompleteFailure)
			.WithData("month", month)
			.WithData("totalEmployees", employeeCount)
			.WithData("firstError", firstError.Error)
			.WithData("firstEmployeeCode", firstError.EmployeeCode);
	}

	public string BuildResponseMessage() =>
		FailedCount > 0
			? $"Payroll generated and locked for {GeneratedCount} staff. {SkippedCount} already finalized. {FailedCount} failed."
			: SkippedCount > 0
				? $"Payroll generated and locked for {GeneratedCount} staff. {SkippedCount} were already finalized."
				: $"Payroll generated and locked successfully for {GeneratedCount} staff.";

	public List<string>? BuildResponseErrors() =>
		FailedCount > 0 ? errors.Select(e => $"{e.EmployeeCode}: {e.Error}").ToList() : null;
}
