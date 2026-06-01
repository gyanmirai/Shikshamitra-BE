using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.AttendanceImports;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Payroll.AttendanceImports;

[RemoteService(false)]
public class AttendanceIntegrationRunAppService(
	IRepository<AttendanceImportBatch, Guid> attendanceImportBatchRepository,
	IRepository<AttendanceImportRow, Guid> attendanceImportRowRepository,
	IEmployeeAttendanceRepository employeeAttendanceRepository,
	IRepository<Employee, Guid> employeeRepository,
	AttendancePolicyWorkedTimeEvaluator workedTimeEvaluator)
	: ApplicationService, IAttendanceIntegrationRunAppService
{
	private const int RowRemarksMaxLength = 512;

	public async Task<AttendanceIntegrationRunResponseDto> RecordIntegrationRunAsync(AttendanceIntegrationRunRequestDto input)
	{
		if (input.Rows == null || input.Rows.Count == 0)
		{
			throw new BusinessException(HRErrorCodes.AttendanceImportNoValidRows);
		}

		var userId = CurrentUser.Id ?? throw new BusinessException(HRErrorCodes.AttendanceImportPreviewInvalid);
		var userName = CurrentUser.UserName ?? "system";
		var batchId = GuidGenerator.Create();
		var parsedRows = input.Rows
			.Select(x => new ParsedIntegrationRow(x, ParseRowDate(x.AttendanceDate)))
			.ToList();

		var firstDate = parsedRows[0].AttendanceDate;
		if (parsedRows.Any(x => x.AttendanceDate.Year != firstDate.Year || x.AttendanceDate.Month != firstDate.Month))
		{
			throw new BusinessException(HRErrorCodes.AttendanceImportMultiplePayrollMonths);
		}

		var monthYear = $"{firstDate:yyyy-MM}";
		var payrollMonthApi = ToApiMonthYear(monthYear);
		var payrollMonthStored = string.IsNullOrWhiteSpace(payrollMonthApi)
			? null
			: (payrollMonthApi.Length > 20 ? payrollMonthApi[..20] : payrollMonthApi);

		var batch = new AttendanceImportBatch(
			batchId,
			CurrentTenant.Id,
			string.IsNullOrWhiteSpace(input.FileName) ? "integration-run" : input.FileName[..Math.Min(input.FileName.Length, 255)],
			userId,
			userName.Length > 128 ? userName[..128] : userName,
			Clock.Now,
			monthYear,
			payrollMonthStored,
			input.Rows.Count,
			input.Rows.Count,
			0,
			0,
			0,
			[],
			[],
			input.RunSource,
			Truncate(input.ExternalCorrelationId, 128),
			Truncate(input.IntegrationProvider, 128),
			Clock.Now,
			null,
			false);

		await attendanceImportBatchRepository.InsertAsync(batch, autoSave: true);

		var employeeIds = parsedRows.Select(r => r.Row.EmployeeId).Distinct().ToList();
		var employeeQuery = await employeeRepository.GetQueryableAsync();
		if (CurrentTenant.Id.HasValue)
		{
			employeeQuery = employeeQuery.Where(x => x.TenantId == CurrentTenant.Id);
		}

		var employees = await AsyncExecuter.ToListAsync(employeeQuery.Where(x => employeeIds.Contains(x.Id)));
		var employeeById = employees.ToDictionary(x => x.Id);
		var dates = parsedRows.Select(r => r.AttendanceDate).ToList();
		var minDate = dates.Min();
		var maxDate = dates.Max();

		var attendanceQuery = await employeeAttendanceRepository.GetQueryableAsync();
		if (CurrentTenant.Id.HasValue)
		{
			attendanceQuery = attendanceQuery.Where(x => x.TenantId == CurrentTenant.Id);
		}

		var existingRows = await AsyncExecuter.ToListAsync(
			attendanceQuery.Where(x =>
				employeeIds.Contains(x.EmployeeId) &&
				x.AttendanceDate >= minDate &&
				x.AttendanceDate <= maxDate));

		var existingLookup = existingRows.ToDictionary(x => $"{x.EmployeeId:N}|{x.AttendanceDate:yyyy-MM-dd}", x => x);

		var created = 0;
		var replaced = 0;
		var rowNumber = 0;
		var importRows = new List<AttendanceImportRow>();

		foreach (var parsedRow in parsedRows)
		{
			var row = parsedRow.Row;
			rowNumber++;
			var dateOnly = parsedRow.AttendanceDate;
			var normalizedRemarks = Truncate(row.Remarks, RowRemarksMaxLength);
			var key = $"{row.EmployeeId:N}|{dateOnly:yyyy-MM-dd}";
			var isReplace = existingLookup.ContainsKey(key);
			var action = isReplace ? "replace" : "create";
			if (!employeeById.TryGetValue(row.EmployeeId, out var employee))
			{
				throw new BusinessException(HRErrorCodes.EmployeeNotFound)
					.WithData("employeeId", row.EmployeeId);
			}

			var tIn = ParseNullableOffset(row.TimeIn);
			var tOut = ParseNullableOffset(row.TimeOut);
			var evaluation = await workedTimeEvaluator.EvaluateAsync(
				employee,
				dateOnly,
				tIn,
				tOut,
				row.Status);
			var status = evaluation.Status;

			if (existingLookup.TryGetValue(key, out var existing))
			{
				existing.TimeIn = tIn ?? existing.TimeIn;
				existing.TimeOut = tOut ?? existing.TimeOut;
				existing.StatusType = status;
				existing.EducationMediumStageId = row.EducationMediumStageId;
				existing.Remarks = normalizedRemarks;
				await employeeAttendanceRepository.UpdateAsync(existing, autoSave: false);
				replaced++;
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
				created++;
			}

			importRows.Add(new AttendanceImportRow(
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
				string.IsNullOrWhiteSpace(evaluation.Warning) ? [] : [evaluation.Warning]));
		}

		if (importRows.Count > 0)
		{
			await attendanceImportRowRepository.InsertManyAsync(importRows, autoSave: false);
		}

		var persisted = await attendanceImportBatchRepository.FindAsync(batchId);
		if (persisted != null)
		{
			persisted.SetRunTiming(batch.RunStartedAt, Clock.Now);
			persisted.MarkConfirmed(input.Rows.Count, created, replaced, Clock.Now);
			await attendanceImportBatchRepository.UpdateAsync(persisted, autoSave: true);
		}

		return new AttendanceIntegrationRunResponseDto
		{
			BatchId = batchId,
			CreatedCount = created,
			ReplacedCount = replaced
		};
	}

	private static string? Truncate(string? value, int max)
	{
		if (string.IsNullOrEmpty(value) || value.Length <= max)
		{
			return value;
		}

		return value[..max];
	}

	private static DateTimeOffset? ParseNullableOffset(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return null;
		}

		if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
		{
			return parsed;
		}

		throw new BusinessException(
			HRErrorCodes.AttendancePolicyEvaluationInvalidPunches,
			"Attendance time is invalid.");
	}

	private readonly record struct ParsedIntegrationRow(
		AttendanceIntegrationRunRowDto Row,
		DateOnly AttendanceDate);

	private static DateOnly ParseRowDate(string attendanceDate)
	{
		if (DateTime.TryParseExact(attendanceDate, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
		{
			return DateOnly.FromDateTime(dt);
		}

		if (DateTime.TryParse(attendanceDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
		{
			return DateOnly.FromDateTime(dt);
		}

		throw new BusinessException(HRErrorCodes.AttendanceInvalidDateFormat);
	}

	private static string? ToApiMonthYear(string? internalMonthYear)
	{
		if (string.IsNullOrWhiteSpace(internalMonthYear))
		{
			return null;
		}

		if (!DateTime.TryParseExact(
			 $"{internalMonthYear}-01",
			 "yyyy-MM-dd",
			 CultureInfo.InvariantCulture,
			 DateTimeStyles.None,
			 out var monthDate))
		{
			return null;
		}

		return $"{monthDate.ToString("MMMM", CultureInfo.InvariantCulture).ToUpperInvariant()}-{monthDate.Year}";
	}
}
