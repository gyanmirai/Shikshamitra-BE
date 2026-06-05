using ShikshaMitra.HR;
using ShikshaMitra.HR.AttendanceAndLeaves.Import;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

internal static class AttendanceImportPreviewParsing
{
	public static AttendanceImportPreviewRow MapRawToPreviewRow(AttendanceImportRawRow rawRow)
	{
		var previewRow = new AttendanceImportPreviewRow
		{
			RowNumber = rawRow.RowNumber,
			AttendanceDate = rawRow.AttendanceDate?.Trim() ?? string.Empty,
			EmployeeCode = rawRow.EmployeeCode?.Trim() ?? string.Empty,
			EmployeeName = rawRow.EmployeeName?.Trim(),
			ContactNo = rawRow.ContactNo?.Trim(),
			TimeIn = rawRow.TimeIn?.Trim(),
			TimeOut = rawRow.TimeOut?.Trim(),
			Status = rawRow.Status?.Trim(),
			Remarks = rawRow.Remarks?.Trim()
		};

		if (string.IsNullOrWhiteSpace(previewRow.AttendanceDate))
		{
			previewRow.Errors.Add("Attendance Date is required.");
		}
		else if (AttendanceImportCellParser.TryParseDate(rawRow.AttendanceDateCell ?? rawRow.AttendanceDate!, out var attendanceDate))
		{
			previewRow.AttendanceDateValue = attendanceDate;
			previewRow.AttendanceDate = attendanceDate.ToString("dd-MM-yyyy");
		}
		else
		{
			previewRow.Errors.Add("Attendance Date is invalid. Use dd-MM-yyyy or ISO date format.");
		}

		if (string.IsNullOrWhiteSpace(previewRow.EmployeeCode))
		{
			previewRow.Errors.Add("Employee Code is required.");
		}

		MapTimeAndStatus(rawRow, previewRow);
		AttendanceImportPreviewRowValidator.ValidateColumnLengths(previewRow);
		return previewRow;
	}

	private static void MapTimeAndStatus(AttendanceImportRawRow rawRow, AttendanceImportPreviewRow previewRow)
	{
		var hasTimeIn = AttendanceImportCellParser.TryParseTime(rawRow.TimeInCell ?? rawRow.TimeIn, out var timeIn);
		var hasTimeOut = AttendanceImportCellParser.TryParseTime(rawRow.TimeOutCell ?? rawRow.TimeOut, out var timeOut);
		var hasAnyTime = hasTimeIn || hasTimeOut;
		var hasBothTimes = hasTimeIn && hasTimeOut;

		if (hasBothTimes)
		{
			if (previewRow.AttendanceDateValue.HasValue)
			{
				var timeInOffset = AttendanceImportCellParser.CombineDateAndTime(previewRow.AttendanceDateValue.Value, timeIn);
				var timeOutOffset = AttendanceImportCellParser.CombineDateAndTime(previewRow.AttendanceDateValue.Value, timeOut);
				if (timeOutOffset <= timeInOffset)
				{
					previewRow.Errors.Add("Time Out must be after Time In.");
				}
				else
				{
					previewRow.TimeInValue = timeInOffset;
					previewRow.TimeOutValue = timeOutOffset;
					previewRow.TimeIn = timeIn.ToString(@"hh\:mm");
					previewRow.TimeOut = timeOut.ToString(@"hh\:mm");
					previewRow.StatusValue = EmployeeAttendanceStatusType.Present;
					previewRow.Status = nameof(EmployeeAttendanceStatusType.Present);
				}
			}
		}
		else if (hasAnyTime)
		{
			if (previewRow.AttendanceDateValue.HasValue)
			{
				if (hasTimeIn)
				{
					previewRow.TimeInValue = AttendanceImportCellParser.CombineDateAndTime(previewRow.AttendanceDateValue.Value, timeIn);
					previewRow.TimeIn = timeIn.ToString(@"hh\:mm");
				}

				if (hasTimeOut)
				{
					previewRow.TimeOutValue = AttendanceImportCellParser.CombineDateAndTime(previewRow.AttendanceDateValue.Value, timeOut);
					previewRow.TimeOut = timeOut.ToString(@"hh\:mm");
				}
			}

			if (string.IsNullOrWhiteSpace(previewRow.Status))
			{
				previewRow.StatusValue = EmployeeAttendanceStatusType.Present;
				previewRow.Status = nameof(EmployeeAttendanceStatusType.Present);
			}
			else if (AttendanceImportCellParser.TryParseStatus(previewRow.Status, out var statusValue))
			{
				previewRow.StatusValue = statusValue;
				previewRow.Status = statusValue.ToString();
			}
			else
			{
				previewRow.Errors.Add("Status is invalid. Allowed values: Present, HalfDayLeave, FullDayLeave.");
			}
		}
		else
		{
			if (string.IsNullOrWhiteSpace(previewRow.Status))
			{
				previewRow.Errors.Add("Status is required when Time In and Time Out are empty.");
			}
			else if (AttendanceImportCellParser.TryParseStatus(previewRow.Status, out var statusValue))
			{
				if (statusValue == EmployeeAttendanceStatusType.Present)
				{
					previewRow.Errors.Add("Present status requires both Time In and Time Out.");
				}
				else
				{
					previewRow.StatusValue = statusValue;
					previewRow.Status = statusValue.ToString();
				}
			}
			else
			{
				previewRow.Errors.Add("Status is invalid. Allowed values: Present, HalfDayLeave, FullDayLeave.");
			}
		}

		if (hasBothTimes && !string.IsNullOrWhiteSpace(rawRow.Status))
		{
			previewRow.Errors.Add("Status must be empty when Time In and Time Out are provided.");
		}
	}
}
