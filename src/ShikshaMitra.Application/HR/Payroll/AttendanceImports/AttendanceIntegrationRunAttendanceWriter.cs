using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Employees;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Payroll.AttendanceImports;

[RemoteService(false)]
public class AttendanceIntegrationRunAttendanceWriter(
	IEmployeeAttendanceRepository employeeAttendanceRepository,
	IRepository<Employee, Guid> employeeRepository,
	AttendanceCaptureModeLookupResolver captureModeLookupResolver,
	AttendancePolicyWorkedTimeEvaluator workedTimeEvaluator)
	: ApplicationService, ITransientDependency
{
	private const int RowRemarksMaxLength = 512;

	public async Task<AttendanceIntegrationRunOutcome> UpsertAttendanceAndBuildImportRowsAsync(
		List<ParsedIntegrationRow> parsedRows,
		Guid batchId)
	{
		var employeeIds = parsedRows.Select(r => r.Row.EmployeeId).Distinct().ToList();
		var employeeById = await LoadEmployeesAsync(employeeIds);
		var existingLookup = await LoadExistingAttendanceAsync(employeeIds, parsedRows);
		var punchContextLookup = BuildPunchContextLookup(parsedRows);
		var captureModeLookup = await captureModeLookupResolver.ResolveForPairsAsync(
			parsedRows.Select(x => (x.Row.EmployeeId, x.AttendanceDate)));
		var outcome = new AttendanceIntegrationRunOutcome();
		var rowNumber = 0;

		foreach (var parsedRow in parsedRows)
		{
			rowNumber++;
			punchContextLookup.TryGetValue(parsedRow.Row.EmployeeId, out var punchContext);
			captureModeLookup.TryGetValue((parsedRow.Row.EmployeeId, parsedRow.AttendanceDate), out var captureMode);
			var importRow = await UpsertAttendanceAndBuildImportRowAsync(
				parsedRow,
				batchId,
				rowNumber,
				employeeById,
				existingLookup,
				punchContext,
				captureMode);
			outcome.ImportRows.Add(importRow.Row);
			outcome.Created += importRow.Created ? 1 : 0;
			outcome.Replaced += importRow.Replaced ? 1 : 0;
		}

		return outcome;
	}

	private async Task<Dictionary<Guid, Employee>> LoadEmployeesAsync(List<Guid> employeeIds)
	{
		var employeeQuery = await employeeRepository.GetQueryableAsync();
		var employees = await AsyncExecuter.ToListAsync(employeeQuery.Where(x => employeeIds.Contains(x.Id)));
		return employees.ToDictionary(x => x.Id);
	}

	private async Task<Dictionary<string, EmployeeAttendance>> LoadExistingAttendanceAsync(
		List<Guid> employeeIds,
		List<ParsedIntegrationRow> parsedRows)
	{
		var dates = parsedRows.Select(r => r.AttendanceDate).ToList();
		var minDate = dates.Min();
		var maxDate = dates.Max();

		var attendanceQuery = await employeeAttendanceRepository.GetQueryableAsync();
		var existingRows = await AsyncExecuter.ToListAsync(
			attendanceQuery.Where(x =>
				employeeIds.Contains(x.EmployeeId) &&
				x.AttendanceDate >= minDate &&
				x.AttendanceDate <= maxDate));

		return existingRows.ToDictionary(x => BuildAttendanceKey(x.EmployeeId, x.AttendanceDate), x => x);
	}

	private async Task<AttendanceIntegrationImportRowOutcome> UpsertAttendanceAndBuildImportRowAsync(
		ParsedIntegrationRow parsedRow,
		Guid batchId,
		int rowNumber,
		Dictionary<Guid, Employee> employeeById,
		Dictionary<string, EmployeeAttendance> existingLookup,
		IReadOnlyCollection<AttendancePolicyEvaluationPunchContext>? punchContext,
		AttendanceCaptureMode captureMode)
	{
		var row = parsedRow.Row;
		var dateOnly = parsedRow.AttendanceDate;
		var normalizedRemarks = AttendanceIntegrationRunParsing.Truncate(row.Remarks, RowRemarksMaxLength);
		var key = BuildAttendanceKey(row.EmployeeId, dateOnly);
		var action = existingLookup.ContainsKey(key) ? "replace" : "create";
		if (!employeeById.TryGetValue(row.EmployeeId, out var employee))
		{
			throw new BusinessException(HRErrorCodes.EmployeeNotFound)
				.WithData("employeeId", row.EmployeeId);
		}

		var tIn = AttendanceIntegrationRunParsing.NormalizeNullableOffset(row.TimeIn, dateOnly);
		var tOut = AttendanceIntegrationRunParsing.NormalizeNullableOffset(row.TimeOut, dateOnly);
		var status = row.Status;
		IReadOnlyList<string> warnings = [];
		if (captureMode == AttendanceCaptureMode.ManualStatus)
		{
			tIn = null;
			tOut = null;
		}
		else
		{
			var evaluation = await workedTimeEvaluator.EvaluateAsync(employee, dateOnly, tIn, tOut, status, punchContext);
			status = evaluation.Status;
			warnings = evaluation.Warnings;
		}

		var replaced = existingLookup.TryGetValue(key, out var existing);
		if (replaced)
		{
			existing!.TimeIn = tIn;
			existing.TimeOut = tOut;
			existing.StatusType = status;
			existing.Source = AttendanceRecordSource.Integration;
			existing.EducationMediumStageId = row.EducationMediumStageId;
			existing.Remarks = normalizedRemarks;
			await employeeAttendanceRepository.UpdateAsync(existing, autoSave: false);
		}
		else
		{
			await employeeAttendanceRepository.InsertAsync(new EmployeeAttendance
			{
				TenantId = CurrentTenant.Id,
				EmployeeId = row.EmployeeId,
				AttendanceDate = dateOnly,
				TimeIn = tIn,
				TimeOut = tOut,
				StatusType = status,
				Source = AttendanceRecordSource.Integration,
				EducationMediumStageId = row.EducationMediumStageId,
				Remarks = normalizedRemarks
			}, autoSave: false);
		}

		return new AttendanceIntegrationImportRowOutcome(
			new AttendanceImportRow(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				batchId,
				rowNumber,
				action,
				row.AttendanceDate,
				string.Empty,
				null,
				null,
				row.TimeIn,
				row.TimeOut,
				status.ToString(),
				normalizedRemarks,
				[],
				[.. warnings]),
			Created: !replaced,
			Replaced: replaced);
	}

	private static string BuildAttendanceKey(Guid employeeId, DateOnly attendanceDate) =>
		$"{employeeId:N}|{attendanceDate:yyyy-MM-dd}";

	private static Dictionary<Guid, IReadOnlyCollection<AttendancePolicyEvaluationPunchContext>> BuildPunchContextLookup(
		List<ParsedIntegrationRow> parsedRows) =>
		parsedRows
			.Select(row => new
			{
				row.Row.EmployeeId,
				row.AttendanceDate,
				TimeIn = AttendanceIntegrationRunParsing.NormalizeNullableOffset(row.Row.TimeIn, row.AttendanceDate)
			})
			.Where(x => x.TimeIn.HasValue)
			.GroupBy(x => x.EmployeeId)
			.ToDictionary(
				x => x.Key,
				x => (IReadOnlyCollection<AttendancePolicyEvaluationPunchContext>)x
					.Select(row => new AttendancePolicyEvaluationPunchContext(row.AttendanceDate, row.TimeIn!.Value))
					.ToList());
}
