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
		var outcome = new AttendanceIntegrationRunOutcome();
		var rowNumber = 0;

		foreach (var parsedRow in parsedRows)
		{
			rowNumber++;
			var importRow = await UpsertAttendanceAndBuildImportRowAsync(parsedRow, batchId, rowNumber, employeeById, existingLookup);
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
		Dictionary<string, EmployeeAttendance> existingLookup)
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

		var tIn = AttendanceIntegrationRunParsing.ParseNullableOffset(row.TimeIn);
		var tOut = AttendanceIntegrationRunParsing.ParseNullableOffset(row.TimeOut);
		var evaluation = await workedTimeEvaluator.EvaluateAsync(employee, dateOnly, tIn, tOut, row.Status);
		var status = evaluation.Status;

		var replaced = existingLookup.TryGetValue(key, out var existing);
		if (replaced)
		{
			existing!.TimeIn = tIn ?? existing.TimeIn;
			existing.TimeOut = tOut ?? existing.TimeOut;
			existing.StatusType = status;
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
				string.IsNullOrWhiteSpace(evaluation.Warning) ? [] : [evaluation.Warning]),
			Created: !replaced,
			Replaced: replaced);
	}

	private static string BuildAttendanceKey(Guid employeeId, DateOnly attendanceDate) =>
		$"{employeeId:N}|{attendanceDate:yyyy-MM-dd}";
}
