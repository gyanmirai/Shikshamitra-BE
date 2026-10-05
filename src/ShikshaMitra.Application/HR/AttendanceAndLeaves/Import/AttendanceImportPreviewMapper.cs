using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.HR.AttendanceAndLeaves.Import;
using Volo.Abp;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

internal static class AttendanceImportPreviewMapper
{
	public const int RowActionMaxLength = 32;
	public const int RowAttendanceDateMaxLength = 32;
	public const int RowEmployeeCodeMaxLength = 64;
	public const int RowEmployeeNameMaxLength = 256;
	public const int RowContactNoMaxLength = 64;
	public const int RowTimeMaxLength = 32;
	public const int RowStatusMaxLength = 64;
	public const int RowRemarksMaxLength = 512;

	public static AttendanceImportPreviewRow MapRawToPreviewRow(AttendanceImportRawRow rawRow) =>
		AttendanceImportPreviewParsing.MapRawToPreviewRow(rawRow);

	public static void MarkDuplicateRows(List<AttendanceImportPreviewRow> rows)
	{
		var duplicateGroups = rows
			.Where(x => x.Errors.Count == 0 && x.AttendanceDateValue.HasValue && !string.IsNullOrWhiteSpace(x.EmployeeCode))
			.GroupBy(x => $"{x.EmployeeCode.Trim().ToUpperInvariant()}|{x.AttendanceDateValue:yyyy-MM-dd}")
			.Where(g => g.Count() > 1);

		foreach (var group in duplicateGroups)
		{
			foreach (var row in group)
			{
				row.Errors.Add("Duplicate Staff Code + Attendance Date found in the file.");
			}
		}
	}

	public static void EnsureSinglePayrollMonth(List<AttendanceImportPreviewRow> previewRows)
	{
		var distinctMonthKeys = previewRows
			.Where(x => x.AttendanceDateValue.HasValue)
			.Select(x => $"{x.AttendanceDateValue!.Value.Year:D4}-{x.AttendanceDateValue.Value.Month:D2}")
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();

		if (distinctMonthKeys.Count > 1)
		{
			throw new BusinessException(HRErrorCodes.AttendanceImportMultiplePayrollMonths);
		}
	}

	public static string? ResolveInternalPayrollMonthYear(List<AttendanceImportPreviewRow> previewRows)
	{
		var rowWithDate = previewRows.FirstOrDefault(x => x.AttendanceDateValue.HasValue);
		if (rowWithDate?.AttendanceDateValue == null)
		{
			return null;
		}

		var date = rowWithDate.AttendanceDateValue.Value;
		return $"{date.Year:D4}-{date.Month:D2}";
	}

	public static AttendanceImportRowResultDto MapToResultDto(AttendanceImportPreviewRow row)
	{
		return new AttendanceImportRowResultDto
		{
			RowNumber = row.RowNumber,
			Action = row.Action,
			AttendanceDate = row.AttendanceDate,
			EmployeeCode = row.EmployeeCode,
			EmployeeName = row.EmployeeName,
			ContactNo = row.ContactNo,
			TimeIn = row.TimeIn,
			TimeOut = row.TimeOut,
			Status = row.Status,
			Remarks = row.Remarks,
			Errors = row.Errors,
			Warnings = row.Warnings
		};
	}

	public static string BuildRowKey(Guid employeeId, DateOnly attendanceDate) =>
		$"{employeeId:N}|{attendanceDate:yyyy-MM-dd}";

	public static string NormalizeComparableValue(string? value) =>
		string.Join(" ", (value ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));

	public static string? TruncateForStorage(string? value, int maxLength)
	{
		if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
		{
			return value;
		}

		return value[..maxLength];
	}

}
