using System.Collections.Generic;
using ShikshaMitra.HR.AttendanceAndLeaves.Import;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

internal static class AttendanceImportPreviewRowValidator
{
	public static void ValidateColumnLengths(AttendanceImportPreviewRow row)
	{
		ValidateMaxLength(row.Action, AttendanceImportPreviewMapper.RowActionMaxLength, "Action", row.Errors);
		ValidateMaxLength(row.AttendanceDate, AttendanceImportPreviewMapper.RowAttendanceDateMaxLength, "Attendance Date", row.Errors);
		ValidateMaxLength(row.EmployeeCode, AttendanceImportPreviewMapper.RowEmployeeCodeMaxLength, "Staff Code", row.Errors);
		ValidateMaxLength(row.EmployeeName, AttendanceImportPreviewMapper.RowEmployeeNameMaxLength, "Staff Name", row.Errors);
		ValidateMaxLength(row.ContactNo, AttendanceImportPreviewMapper.RowContactNoMaxLength, "Contact No", row.Errors);
		ValidateMaxLength(row.TimeIn, AttendanceImportPreviewMapper.RowTimeMaxLength, "Time In", row.Errors);
		ValidateMaxLength(row.TimeOut, AttendanceImportPreviewMapper.RowTimeMaxLength, "Time Out", row.Errors);
		ValidateMaxLength(row.Status, AttendanceImportPreviewMapper.RowStatusMaxLength, "Status", row.Errors);
		ValidateMaxLength(row.Remarks, AttendanceImportPreviewMapper.RowRemarksMaxLength, "Remarks", row.Errors);
	}

	private static void ValidateMaxLength(string? value, int maxLength, string fieldName, ICollection<string> errors)
	{
		if (!string.IsNullOrEmpty(value) && value.Length > maxLength)
		{
			errors.Add($"{fieldName} exceeds maximum length of {maxLength} characters.");
		}
	}
}
