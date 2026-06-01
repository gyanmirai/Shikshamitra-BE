using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.AttendanceAndLeaves.TakeAttendance;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll;
using ShikshaMitra.HR.Payroll.SalarySlips;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Payroll.AttendanceImports;

[RemoteService(false)]
public class AttendanceImportLogAppService(
	IRepository<AttendanceImportBatch, Guid> attendanceImportBatchRepository,
	IRepository<AttendanceImportRow, Guid> attendanceImportRowRepository,
	IRepository<Employee, Guid> employeeRepository,
	ISalarySlipRepository salarySlipRepository,
	ITakeAttendanceAppService takeAttendanceAppService,
	AttendancePolicyWorkedTimeEvaluator workedTimeEvaluator)
	: ApplicationService, IAttendanceImportLogAppService
{
	private static readonly TimeZoneInfo DefaultImportTimeZone = TimeZoneInfo.Local;

	public async Task<PagedResultDto<AttendanceImportLogListItemDto>> GetListAsync(GetAttendanceImportLogListInput input)
	{
		ValidatePaging(input.SkipCount, input.MaxResultCount, maxUpperBound: 200);

		var query = await attendanceImportBatchRepository.GetQueryableAsync();
		if (CurrentTenant.Id.HasValue)
		{
			query = query.Where(x => x.TenantId == CurrentTenant.Id);
		}

		if (!string.IsNullOrWhiteSpace(input.Search))
		{
			var search = input.Search.Trim();
			query = query.Where(x =>
				x.FileName.Contains(search) ||
				x.ImportedByName.Contains(search));
		}

		if (!string.IsNullOrWhiteSpace(input.PayrollMonth))
		{
			if (!PayrollMonthNormalizer.TryNormalizeToInternal(input.PayrollMonth, out var normalizedPayrollMonth))
			{
				throw new BusinessException(HRErrorCodes.InvalidMonthYearFormat)
					.WithData("providedMonth", input.PayrollMonth)
					.WithData("acceptedFormats", "YYYY-MM, MMMM-YYYY, or date-like values");
			}

			query = query.Where(x => x.PayrollMonthYear == normalizedPayrollMonth);
		}

		var totalCount = await AsyncExecuter.CountAsync(query);
		var skipCount = Math.Max(0, input.SkipCount);
		var maxResultCount = input.MaxResultCount <= 0 ? 50 : input.MaxResultCount;
		var items = await AsyncExecuter.ToListAsync(
			query
				.OrderByDescending(x => x.ImportedAt)
				.Skip(skipCount)
				.Take(maxResultCount));

		var finalizedMonthLookup = await BuildFinalizedMonthLookupAsync(items);
		return new PagedResultDto<AttendanceImportLogListItemDto>(
			totalCount,
			items.Select(x => MapToListItemDto(x, finalizedMonthLookup)).ToList());
	}

	public async Task<AttendanceImportLogDetailDto> GetAsync(Guid importId)
	{
		var entity = await attendanceImportBatchRepository.FindAsync(importId)
			?? throw new EntityNotFoundException(typeof(AttendanceImportBatch), importId);

		EnsureTenantOwnership(entity, importId);

		var finalizedMonthLookup = await BuildFinalizedMonthLookupAsync([entity]);
		var baseDto = MapToListItemDto(entity, finalizedMonthLookup);
		return new AttendanceImportLogDetailDto
		{
			Id = baseDto.Id,
			FileName = baseDto.FileName,
			ImportedByUserId = baseDto.ImportedByUserId,
			ImportedByName = baseDto.ImportedByName,
			ImportedAt = baseDto.ImportedAt,
			Status = baseDto.Status,
			PayrollMonth = baseDto.PayrollMonth,
			TotalRows = baseDto.TotalRows,
			ValidRows = baseDto.ValidRows,
			InvalidRows = baseDto.InvalidRows,
			CreateCount = baseDto.CreateCount,
			ReplaceCount = baseDto.ReplaceCount,
			ProcessedRows = baseDto.ProcessedRows,
			CreatedCount = baseDto.CreatedCount,
			ReplacedCount = baseDto.ReplacedCount,
			CanOpen = baseDto.CanOpen,
			RunSource = baseDto.RunSource,
			ExternalCorrelationId = baseDto.ExternalCorrelationId,
			IntegrationProvider = baseDto.IntegrationProvider,
			IsBatchLocked = baseDto.IsBatchLocked,
			ConfirmedAt = entity.ConfirmedAt,
			PreviewTokenExpiresAt = entity.PreviewTokenExpiresAt,
			ErrorsSummary = [.. entity.ErrorsSummary],
			WarningsSummary = [.. entity.WarningsSummary]
		};
	}

	public async Task<PagedResultDto<AttendanceImportRowDto>> GetRowsAsync(Guid importId, GetAttendanceImportRowsInput input)
	{
		ValidatePaging(input.SkipCount, input.MaxResultCount, maxUpperBound: 500);

		var batch = await attendanceImportBatchRepository.FindAsync(importId)
			?? throw new EntityNotFoundException(typeof(AttendanceImportBatch), importId);
		EnsureTenantOwnership(batch, importId);

		var query = await attendanceImportRowRepository.GetQueryableAsync();
		query = query.Where(x => x.BatchId == importId);
		if (CurrentTenant.Id.HasValue)
		{
			query = query.Where(x => x.TenantId == CurrentTenant.Id);
		}

		var totalCount = await AsyncExecuter.CountAsync(query);
		var skipCount = Math.Max(0, input.SkipCount);
		var maxResultCount = input.MaxResultCount <= 0 ? 50 : input.MaxResultCount;
		var items = await AsyncExecuter.ToListAsync(
			query
				.OrderBy(x => x.RowNumber)
				.Skip(skipCount)
				.Take(maxResultCount));
		var employeeLookup = await BuildEmployeeLookupByCodeAsync(items);

		return new PagedResultDto<AttendanceImportRowDto>(
			totalCount,
			items.Select(row => MapToRowDto(row, employeeLookup)).ToList());
	}

	public async Task<AttendanceImportRowsSaveOutput> SaveRowsAsync(Guid importId, AttendanceImportRowsSaveInput input)
	{
		if (input.Entries == null || input.Entries.Count == 0)
		{
			throw new BusinessException(HRErrorCodes.TakeAttendanceEmptyEntries);
		}

		if (input.Entries.Select(x => x.RowId).Distinct().Count() != input.Entries.Count)
		{
			throw new BusinessException(HRErrorCodes.AttendanceImportRowMismatch)
				.WithData("reason", "Duplicate import row ids are not allowed.");
		}

		var batch = await GetOwnedBatchAsync(importId);
		if (batch.IsBatchLocked)
		{
			throw new BusinessException(HRErrorCodes.AttendanceImportBatchLocked);
		}

		if (!string.IsNullOrWhiteSpace(batch.PayrollMonthYear) &&
		    await salarySlipRepository.HasFinalizedSlipsForMonthAsync(batch.PayrollMonthYear!))
		{
			throw new BusinessException(HRErrorCodes.AttendanceImportPayrollLocked);
		}

		var requestedRowIds = input.Entries.Select(x => x.RowId).ToList();
		var rowsQuery = await attendanceImportRowRepository.GetQueryableAsync();
		rowsQuery = rowsQuery.Where(x => x.BatchId == importId && requestedRowIds.Contains(x.Id));
		if (CurrentTenant.Id.HasValue)
		{
			rowsQuery = rowsQuery.Where(x => x.TenantId == CurrentTenant.Id);
		}

		var importRows = await AsyncExecuter.ToListAsync(rowsQuery);
		if (importRows.Count != requestedRowIds.Count)
		{
			throw new BusinessException(HRErrorCodes.AttendanceImportRowMismatch);
		}

		var employeeLookup = await BuildEmployeeLookupByCodeAsync(importRows);
		var rowLookup = importRows.ToDictionary(x => x.Id);
		var normalizedEntries = input.Entries
			.Select(entry => NormalizeSaveEntry(entry, rowLookup[entry.RowId], employeeLookup))
			.ToList();
		normalizedEntries = await EvaluateSaveEntriesAsync(normalizedEntries);

		if (normalizedEntries
		    .Select(x => $"{x.EmployeeId:N}|{x.AttendanceDate:yyyy-MM-dd}")
		    .Distinct(StringComparer.OrdinalIgnoreCase)
		    .Count() != normalizedEntries.Count)
		{
			throw new BusinessException(HRErrorCodes.AttendanceImportRowMismatch)
				.WithData("reason", "Duplicate employee/date entries are not allowed.");
		}

		var entriesByDate = normalizedEntries
			.GroupBy(x => x.AttendanceDate)
			.ToList();
		var upsertedCount = 0;
		foreach (var dateGroup in entriesByDate)
		{
			var saveOutput = await takeAttendanceAppService.SaveDayAsync(new TakeAttendanceSaveInput
			{
				AttendanceDate = dateGroup.Key.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture),
				Entries = dateGroup.Select(entry => new TakeAttendanceSaveEntryDto
				{
					EmployeeId = entry.EmployeeId,
					StatusWire = entry.SaveStatusWire,
					InTime = entry.InTimeForSave,
					OutTime = entry.OutTimeForSave,
					Remarks = entry.Remarks
				}).ToList()
			});
			upsertedCount += saveOutput.UpsertedCount;
		}

		foreach (var entry in normalizedEntries)
		{
			rowLookup[entry.RowId].UpdateEditableValues(
				entry.InTimeForImportRow,
				entry.OutTimeForImportRow,
				entry.ImportRowStatus,
				entry.Remarks,
				entry.Warning);
		}

		await attendanceImportRowRepository.UpdateManyAsync(importRows, autoSave: true);
		return new AttendanceImportRowsSaveOutput { UpsertedCount = upsertedCount };
	}

	public async Task LockBatchAsync(Guid importId)
	{
		var batch = await GetOwnedBatchAsync(importId);
		if (batch.IsBatchLocked)
		{
			return;
		}

		batch.SetBatchLocked(true);
		await attendanceImportBatchRepository.UpdateAsync(batch, autoSave: true);
	}

	public async Task UnlockBatchAsync(Guid importId)
	{
		var batch = await GetOwnedBatchAsync(importId);
		if (!batch.IsBatchLocked)
		{
			return;
		}

		batch.SetBatchLocked(false);
		await attendanceImportBatchRepository.UpdateAsync(batch, autoSave: true);
	}

	private async Task<Dictionary<string, bool>> BuildFinalizedMonthLookupAsync(IEnumerable<AttendanceImportBatch> items)
	{
		var monthYears = items
			.Select(x => x.PayrollMonthYear)
			.Where(x => !string.IsNullOrWhiteSpace(x))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();

		var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
		foreach (var monthYear in monthYears)
		{
			result[monthYear!] = await salarySlipRepository.HasFinalizedSlipsForMonthAsync(monthYear!);
		}

		return result;
	}

	private static AttendanceImportLogListItemDto MapToListItemDto(
		AttendanceImportBatch entity,
		Dictionary<string, bool> finalizedMonthLookup)
	{
		var isPayrollLocked = entity.Status == AttendanceImportBatchStatus.Confirmed &&
			!string.IsNullOrWhiteSpace(entity.PayrollMonthYear) &&
			finalizedMonthLookup.GetValueOrDefault(entity.PayrollMonthYear!, false);

		var status = entity.Status switch
		{
			AttendanceImportBatchStatus.Preview => AttendanceImportLogStatus.Preview,
			AttendanceImportBatchStatus.Confirmed when isPayrollLocked => AttendanceImportLogStatus.PayrollLocked,
			AttendanceImportBatchStatus.Confirmed when entity.IsBatchLocked => AttendanceImportLogStatus.BatchLocked,
			AttendanceImportBatchStatus.Confirmed => AttendanceImportLogStatus.Confirmed,
			_ => AttendanceImportLogStatus.Preview
		};

		return new AttendanceImportLogListItemDto
		{
			Id = entity.Id,
			FileName = entity.FileName,
			ImportedByUserId = entity.ImportedByUserId,
			ImportedByName = entity.ImportedByName,
			ImportedAt = entity.ImportedAt,
			Status = status,
			PayrollMonth = entity.PayrollMonth ?? PayrollMonthNormalizer.ToApiMonthYear(entity.PayrollMonthYear),
			TotalRows = entity.TotalRows,
			ValidRows = entity.ValidRows,
			InvalidRows = entity.InvalidRows,
			CreateCount = entity.CreateCount,
			ReplaceCount = entity.ReplaceCount,
			ProcessedRows = entity.ProcessedRows,
			CreatedCount = entity.CreatedCount,
			ReplacedCount = entity.ReplacedCount,
			CanOpen = entity.TotalRows > 0,
			RunSource = entity.RunSource,
			ExternalCorrelationId = entity.ExternalCorrelationId,
			IntegrationProvider = entity.IntegrationProvider,
			IsBatchLocked = entity.IsBatchLocked
		};
	}

	private static AttendanceImportRowDto MapToRowDto(AttendanceImportRow row, Dictionary<string, Guid> employeeLookup)
	{
		var resolvedEmployeeId = TryResolveEmployeeId(row.EmployeeCode, employeeLookup);
		var statusCode = TryNormalizeStatusCode(row.Status, out var normalizedWarning);
		var warnings = row.Warnings.ToList();
		if (!resolvedEmployeeId.HasValue && !string.IsNullOrWhiteSpace(row.EmployeeCode))
		{
			warnings.Add($"Unable to resolve employeeId for employee code '{row.EmployeeCode}'.");
		}

		if (!string.IsNullOrWhiteSpace(normalizedWarning))
		{
			warnings.Add(normalizedWarning);
		}

		return new AttendanceImportRowDto
		{
			Id = row.Id,
			EmployeeId = resolvedEmployeeId,
			RowNumber = row.RowNumber,
			Action = row.Action,
			AttendanceDate = row.AttendanceDate,
			EmployeeCode = row.EmployeeCode,
			EmployeeName = row.EmployeeName,
			ContactNo = row.ContactNo,
			TimeIn = row.TimeIn,
			TimeOut = row.TimeOut,
			Status = row.Status,
			StatusCode = statusCode,
			Remarks = row.Remarks,
			Errors = [.. row.Errors],
			Warnings = warnings
		};
	}

	private async Task<Dictionary<string, Guid>> BuildEmployeeLookupByCodeAsync(List<AttendanceImportRow> rows)
	{
		var employeeCodes = rows
			.Select(row => row.EmployeeCode?.Trim())
			.Where(code => !string.IsNullOrWhiteSpace(code))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();
		if (employeeCodes.Count == 0)
		{
			return new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
		}
		var normalizedEmployeeCodes = employeeCodes
			.Select(code => code!.ToUpperInvariant())
			.ToList();

		var employeeQuery = await employeeRepository.GetQueryableAsync();
		if (CurrentTenant.Id.HasValue)
		{
			employeeQuery = employeeQuery.Where(x => x.TenantId == CurrentTenant.Id);
		}

		var employees = await AsyncExecuter.ToListAsync(
			employeeQuery
				.Where(employee =>
					employee.EmployeeCode != null &&
					normalizedEmployeeCodes.Contains(employee.EmployeeCode.ToUpper()))
				.Select(employee => new { employee.EmployeeCode, employee.Id }));
		return employees
			.GroupBy(x => x.EmployeeCode, StringComparer.OrdinalIgnoreCase)
			.ToDictionary(group => group.Key, group => group.First().Id, StringComparer.OrdinalIgnoreCase);
	}

	private static Guid? TryResolveEmployeeId(string? employeeCode, Dictionary<string, Guid> employeeLookup)
	{
		if (string.IsNullOrWhiteSpace(employeeCode))
		{
			return null;
		}

		return employeeLookup.TryGetValue(employeeCode.Trim(), out var employeeId) ? employeeId : null;
	}

	private static AttendanceImportRowStatusCode? TryNormalizeStatusCode(string? status, out string? warning)
	{
		warning = null;
		if (string.IsNullOrWhiteSpace(status))
		{
			return null;
		}

		var normalized = status.Trim().Replace("_", string.Empty).Replace("-", string.Empty).Replace(" ", string.Empty).ToUpperInvariant();
		return normalized switch
		{
			"PRESENT" => AttendanceImportRowStatusCode.Present,
			"ABSENT" => AttendanceImportRowStatusCode.Absent,
			"FULLDAYLEAVE" => AttendanceImportRowStatusCode.Absent,
			"HALFDAY" => AttendanceImportRowStatusCode.HalfDay,
			"HALFDAYLEAVE" => AttendanceImportRowStatusCode.HalfDay,
			"PENDING" => null,
			"NOTMARKED" => null,
			_ => SetStatusWarning(status, out warning)
		};
	}

	private NormalizedAttendanceImportSaveEntry NormalizeSaveEntry(
		AttendanceImportRowsSaveEntryDto entry,
		AttendanceImportRow row,
		Dictionary<string, Guid> employeeLookup)
	{
		var expectedEmployeeId = TryResolveEmployeeId(row.EmployeeCode, employeeLookup);
		if (!expectedEmployeeId.HasValue || expectedEmployeeId.Value != entry.EmployeeId)
		{
			throw new BusinessException(HRErrorCodes.AttendanceImportRowMismatch)
				.WithData("rowId", entry.RowId)
				.WithData("reason", "Employee does not match the import row.");
		}

		var rowDate = ParseImportRowDate(row.AttendanceDate);
		var entryDate = ParseImportRowDate(entry.AttendanceDate);
		if (rowDate != entryDate)
		{
			throw new BusinessException(HRErrorCodes.AttendanceImportRowMismatch)
				.WithData("rowId", entry.RowId)
				.WithData("reason", "Attendance date does not match the import row.");
		}

		var status = NormalizeStatusForImportSave(entry.StatusWire);
		var inTime = NormalizeTimeForSave(entry.InTime, rowDate);
		var outTime = NormalizeTimeForSave(entry.OutTime, rowDate);

		return new NormalizedAttendanceImportSaveEntry(
			entry.RowId,
			entry.EmployeeId,
			rowDate,
			status.SaveWire,
			status.ImportRowStatus,
			inTime.SaveValue,
			outTime.SaveValue,
			inTime.ImportRowValue,
			outTime.ImportRowValue,
			entry.Remarks);
	}

	private async Task<List<NormalizedAttendanceImportSaveEntry>> EvaluateSaveEntriesAsync(
		List<NormalizedAttendanceImportSaveEntry> entries)
	{
		if (entries.Count == 0)
		{
			return entries;
		}

		var employeeIds = entries.Select(x => x.EmployeeId).Distinct().ToList();
		var employeeQuery = await employeeRepository.GetQueryableAsync();
		if (CurrentTenant.Id.HasValue)
		{
			employeeQuery = employeeQuery.Where(x => x.TenantId == CurrentTenant.Id);
		}

		var employees = await AsyncExecuter.ToListAsync(employeeQuery.Where(x => employeeIds.Contains(x.Id)));
		var employeeById = employees.ToDictionary(x => x.Id);
		var result = new List<NormalizedAttendanceImportSaveEntry>(entries.Count);
		foreach (var entry in entries)
		{
			if (!employeeById.TryGetValue(entry.EmployeeId, out var employee))
			{
				throw new BusinessException(HRErrorCodes.AttendanceImportRowMismatch)
					.WithData("rowId", entry.RowId)
					.WithData("reason", "Employee does not match the import row.");
			}

			var evaluation = await workedTimeEvaluator.EvaluateAsync(
				employee,
				entry.AttendanceDate,
				ParseNullableOffset(entry.InTimeForSave),
				ParseNullableOffset(entry.OutTimeForSave),
				ParseDomainStatus(entry.SaveStatusWire));

			result.Add(entry with
			{
				SaveStatusWire = ToSaveWire(evaluation.Status),
				ImportRowStatus = evaluation.Status.ToString(),
				Warning = evaluation.Warning
			});
		}

		return result;
	}

	private static NormalizedAttendanceImportStatus NormalizeStatusForImportSave(string? statusWire)
	{
		return NormalizeStatusKey(statusWire) switch
		{
			"PRESENT" => new NormalizedAttendanceImportStatus("Present", "Present"),
			"ABSENT" or "FULLDAYLEAVE" => new NormalizedAttendanceImportStatus("Absent", "FullDayLeave"),
			"HALFDAY" or "HALFDAYLEAVE" => new NormalizedAttendanceImportStatus("HalfDay", "HalfDayLeave"),
			_ => throw new BusinessException(HRErrorCodes.TakeAttendanceInvalidStatusWire)
		};
	}

	private static EmployeeAttendanceStatusType ParseDomainStatus(string statusWire) =>
		NormalizeStatusKey(statusWire) switch
		{
			"PRESENT" => EmployeeAttendanceStatusType.Present,
			"ABSENT" or "FULLDAYLEAVE" => EmployeeAttendanceStatusType.FullDayLeave,
			"HALFDAY" or "HALFDAYLEAVE" => EmployeeAttendanceStatusType.HalfDayLeave,
			_ => throw new BusinessException(HRErrorCodes.TakeAttendanceInvalidStatusWire)
		};

	private static string ToSaveWire(EmployeeAttendanceStatusType status) =>
		status switch
		{
			EmployeeAttendanceStatusType.Present => "Present",
			EmployeeAttendanceStatusType.HalfDayLeave => "HalfDay",
			_ => "Absent"
		};

	private static DateTimeOffset? ParseNullableOffset(string? value) =>
		string.IsNullOrWhiteSpace(value)
			? null
			: DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

	private static string NormalizeStatusKey(string? value) =>
		(value ?? string.Empty)
			.Trim()
			.Replace("_", string.Empty)
			.Replace("-", string.Empty)
			.Replace(" ", string.Empty)
			.ToUpperInvariant();

	private static DateOnly ParseImportRowDate(string? value)
	{
		if (DateTime.TryParseExact(
			    (value ?? string.Empty).Trim(),
			    ["dd-MM-yyyy", "d-M-yyyy", "yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy"],
			    CultureInfo.InvariantCulture,
			    DateTimeStyles.None,
			    out var exact))
		{
			return DateOnly.FromDateTime(exact);
		}

		if (DateTime.TryParse(
			    (value ?? string.Empty).Trim(),
			    CultureInfo.InvariantCulture,
			    DateTimeStyles.AllowWhiteSpaces,
			    out var parsed))
		{
			return DateOnly.FromDateTime(parsed);
		}

		throw new BusinessException(HRErrorCodes.AttendanceImportRowMismatch)
			.WithData("reason", "Attendance date is invalid.");
	}

	private static NormalizedAttendanceImportTime NormalizeTimeForSave(string? value, DateOnly attendanceDate)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return new NormalizedAttendanceImportTime(null, null);
		}

		var trimmed = value.Trim();
		if (DateTimeOffset.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsedOffset))
		{
			return new NormalizedAttendanceImportTime(
				parsedOffset.ToString("o", CultureInfo.InvariantCulture),
				parsedOffset.ToString("HH:mm", CultureInfo.InvariantCulture));
		}

		if (TryParseTime(trimmed, out var parsedTime))
		{
			var localDateTime = attendanceDate.ToDateTime(TimeOnly.FromTimeSpan(parsedTime), DateTimeKind.Unspecified);
			var offset = DefaultImportTimeZone.GetUtcOffset(localDateTime);
			var dateTimeOffset = new DateTimeOffset(localDateTime, offset);
			return new NormalizedAttendanceImportTime(
				dateTimeOffset.ToString("o", CultureInfo.InvariantCulture),
				parsedTime.ToString(@"hh\:mm", CultureInfo.InvariantCulture));
		}

		throw new BusinessException(HRErrorCodes.AttendanceImportRowMismatch)
			.WithData("reason", "Attendance time is invalid.");
	}

	private static bool TryParseTime(string value, out TimeSpan time)
	{
		if (DateTime.TryParseExact(
			    value,
			    ["HH:mm", "H:mm", "hh:mm tt", "h:mm tt", "hh:mmtt", "h:mmtt", "HH:mm:ss", "hh:mm:ss tt", "h:mm:ss tt"],
			    CultureInfo.InvariantCulture,
			    DateTimeStyles.AllowWhiteSpaces,
			    out var parsedDateTime))
		{
			time = parsedDateTime.TimeOfDay;
			return true;
		}

		return TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out time);
	}

	private static AttendanceImportRowStatusCode? SetStatusWarning(string originalStatus, out string? warning)
	{
		warning = $"Unable to normalize attendance status '{originalStatus}'.";
		return null;
	}

	private static void ValidatePaging(int skipCount, int maxResultCount, int maxUpperBound)
	{
		if (skipCount < 0)
		{
			throw new BusinessException(HRErrorCodes.AttendanceImportInvalidSkipCount);
		}

		if (maxResultCount <= 0 || maxResultCount > maxUpperBound)
		{
			throw new BusinessException(HRErrorCodes.AttendanceImportInvalidMaxResultCount)
				.WithData("allowedRange", $"1-{maxUpperBound}")
				.WithData("providedValue", maxResultCount);
		}
	}

	private async Task<AttendanceImportBatch> GetOwnedBatchAsync(Guid importId)
	{
		var batch = await attendanceImportBatchRepository.FindAsync(importId)
			?? throw new EntityNotFoundException(typeof(AttendanceImportBatch), importId);
		EnsureTenantOwnership(batch, importId);
		return batch;
	}

	private void EnsureTenantOwnership(AttendanceImportBatch batch, Guid importId)
	{
		if (batch.TenantId != CurrentTenant.Id)
		{
			throw new EntityNotFoundException(typeof(AttendanceImportBatch), importId);
		}
	}

	private sealed record NormalizedAttendanceImportStatus(string SaveWire, string ImportRowStatus);

	private sealed record NormalizedAttendanceImportTime(string? SaveValue, string? ImportRowValue);

	private sealed record NormalizedAttendanceImportSaveEntry(
		Guid RowId,
		Guid EmployeeId,
		DateOnly AttendanceDate,
		string SaveStatusWire,
		string ImportRowStatus,
		string? InTimeForSave,
		string? OutTimeForSave,
		string? InTimeForImportRow,
		string? OutTimeForImportRow,
		string? Remarks,
		string? Warning = null);
}
