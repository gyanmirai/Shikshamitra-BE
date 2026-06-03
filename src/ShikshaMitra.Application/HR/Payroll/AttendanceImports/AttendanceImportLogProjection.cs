using System;
using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.HR.Payroll.AttendanceImports;

internal static class AttendanceImportLogProjection
{
	public static AttendanceImportLogListItemDto ToListItemDto(
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

	public static AttendanceImportLogDetailDto ToDetailDto(
		AttendanceImportBatch entity,
		Dictionary<string, bool> finalizedMonthLookup)
	{
		var baseDto = ToListItemDto(entity, finalizedMonthLookup);
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

	public static AttendanceImportRowDto ToRowDto(AttendanceImportRow row, Dictionary<string, Guid> employeeLookup)
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

	public static Guid? TryResolveEmployeeId(string? employeeCode, Dictionary<string, Guid> employeeLookup)
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

	private static AttendanceImportRowStatusCode? SetStatusWarning(string originalStatus, out string? warning)
	{
		warning = $"Unable to normalize attendance status '{originalStatus}'.";
		return null;
	}
}
