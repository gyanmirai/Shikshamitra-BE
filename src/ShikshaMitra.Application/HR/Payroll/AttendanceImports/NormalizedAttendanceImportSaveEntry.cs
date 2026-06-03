using System;

namespace ShikshaMitra.HR.Payroll.AttendanceImports;

public sealed record NormalizedAttendanceImportSaveEntry(
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

internal sealed record NormalizedAttendanceImportTime(string? SaveValue, string? ImportRowValue);
