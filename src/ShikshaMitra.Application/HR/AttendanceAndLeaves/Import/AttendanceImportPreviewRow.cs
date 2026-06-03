using ShikshaMitra.HR;
using System;
using System.Collections.Generic;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

internal sealed class AttendanceImportPreviewRow
{
	public int RowNumber { get; set; }
	public string Action { get; set; } = "invalid";
	public string AttendanceDate { get; set; } = string.Empty;
	public DateOnly? AttendanceDateValue { get; set; }
	public string EmployeeCode { get; set; } = string.Empty;
	public string? EmployeeName { get; set; }
	public string? ContactNo { get; set; }
	public string? TimeIn { get; set; }
	public DateTimeOffset? TimeInValue { get; set; }
	public string? TimeOut { get; set; }
	public DateTimeOffset? TimeOutValue { get; set; }
	public string? Status { get; set; }
	public EmployeeAttendanceStatusType StatusValue { get; set; }
	public string? Remarks { get; set; }
	public Guid EmployeeId { get; set; }
	public Guid? EducationMediumStageId { get; set; }
	public List<string> Errors { get; } = [];
	public List<string> Warnings { get; } = [];
}
