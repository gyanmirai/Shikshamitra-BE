using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.AttendanceImports;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Import;

public class AttendanceImportPreviewRowEnricher(
	IEmployeeRepository employeeRepository,
	AttendanceCaptureModeLookupResolver captureModeLookupResolver,
	AttendancePolicyWorkedTimeEvaluator workedTimeEvaluator)
	: ITransientDependency
{
	internal async Task EnrichAsync(List<AttendanceImportPreviewRow> rows)
	{
		var employeeCodes = rows
			.Where(x => x.Errors.Count == 0 && !string.IsNullOrWhiteSpace(x.EmployeeCode))
			.Select(x => x.EmployeeCode)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();

		var employeesByCode = await employeeRepository.GetEmployeesByCodesAsync(employeeCodes);
		var matchedRows = new List<(AttendanceImportPreviewRow Row, Employee Employee)>();
		foreach (var row in rows.Where(x => x.Errors.Count == 0))
		{
			if (!employeesByCode.TryGetValue(row.EmployeeCode.ToUpperInvariant(), out var employee))
			{
				row.Errors.Add("Employee Code was not found.");
				continue;
			}

			if (row.AttendanceDateValue.HasValue &&
			    !EmployeeLifecycleEligibility.IsAttendanceEligibleOnDate(
				    employee.EmployeeStatusType,
				    employee.StatusEffectiveDate,
				    row.AttendanceDateValue.Value))
			{
				row.Errors.Add("Employee is not active on the attendance date.");
				continue;
			}

			row.EmployeeId = employee.Id;
			row.EducationMediumStageId = null;
			AddNameMismatchWarning(row, employee);
			matchedRows.Add((row, employee));
		}

		var datedRows = matchedRows
			.Where(x => x.Row.AttendanceDateValue.HasValue)
			.ToList();
		var captureModeLookup = datedRows.Count == 0
			? new Dictionary<(Guid EmployeeId, DateOnly Date), AttendanceCaptureMode>()
			: await captureModeLookupResolver.ResolveForPairsAsync(
				datedRows.Select(x => (x.Employee.Id, x.Row.AttendanceDateValue!.Value)));
		var punchContextLookup = datedRows
			.Where(x => x.Row.TimeInValue.HasValue)
			.GroupBy(x => x.Employee.Id)
			.ToDictionary(
				x => x.Key,
				x => (IReadOnlyCollection<AttendancePolicyEvaluationPunchContext>)x
					.Select(row => new AttendancePolicyEvaluationPunchContext(
						row.Row.AttendanceDateValue!.Value,
						row.Row.TimeInValue!.Value))
					.ToList());

		foreach (var (row, employee) in matchedRows)
		{
			punchContextLookup.TryGetValue(employee.Id, out var punchContext);
			await EvaluatePolicyAsync(row, employee, captureModeLookup, punchContext);
		}
	}

	private static void AddNameMismatchWarning(AttendanceImportPreviewRow row, Employee employee)
	{
		if (string.IsNullOrWhiteSpace(row.EmployeeName))
		{
			return;
		}

		var normalizedIncoming = AttendanceImportPreviewMapper.NormalizeComparableValue(row.EmployeeName);
		var normalizedStored = AttendanceImportPreviewMapper.NormalizeComparableValue(employee.EmployeeNameWithFather);
		if (!string.Equals(normalizedIncoming, normalizedStored, StringComparison.OrdinalIgnoreCase))
		{
			row.Warnings.Add("Employee Name does not match the current employee record.");
		}
	}

	private async Task EvaluatePolicyAsync(
		AttendanceImportPreviewRow row,
		Employee employee,
		Dictionary<(Guid EmployeeId, DateOnly Date), AttendanceCaptureMode> captureModeLookup,
		IReadOnlyCollection<AttendancePolicyEvaluationPunchContext>? punchContext)
	{
		if (!row.AttendanceDateValue.HasValue)
		{
			return;
		}

		try
		{
			if (captureModeLookup.GetValueOrDefault((employee.Id, row.AttendanceDateValue.Value)) == AttendanceCaptureMode.ManualStatus)
			{
				return;
			}

			var evaluation = await workedTimeEvaluator.EvaluateAsync(
				employee,
				row.AttendanceDateValue.Value,
				row.TimeInValue,
				row.TimeOutValue,
				row.StatusValue,
				punchContext);
			row.StatusValue = evaluation.Status;
			row.Status = evaluation.Status.ToString();
			row.Warnings.AddRange(evaluation.Warnings);
		}
		catch (BusinessException ex)
		{
			row.Errors.Add(GetFriendlyBusinessExceptionMessage(ex));
		}
	}

	private static string GetFriendlyBusinessExceptionMessage(BusinessException ex) =>
		string.IsNullOrWhiteSpace(ex.Message) ||
		ex.Message == ex.Code ||
		ex.Message.StartsWith("Exception of type", StringComparison.OrdinalIgnoreCase)
			? ex.Code ?? "Attendance policy evaluation failed."
			: ex.Message;
}
