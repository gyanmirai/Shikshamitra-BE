namespace ShikshaMitra.HR.AttendanceAndLeaves;

internal sealed class AttendanceImportRawRow
{
	public int RowNumber { get; set; }
	public string? AttendanceDate { get; set; }
	public string? EmployeeCode { get; set; }
	public string? TimeIn { get; set; }
	public string? TimeOut { get; set; }
	public string? Status { get; set; }
	public string? Remarks { get; set; }
	public string? EmployeeName { get; set; }
	public string? ContactNo { get; set; }
	public object? AttendanceDateCell { get; set; }
	public object? TimeInCell { get; set; }
	public object? TimeOutCell { get; set; }

	public bool IsEmpty() =>
		string.IsNullOrWhiteSpace(AttendanceDate) &&
		string.IsNullOrWhiteSpace(EmployeeCode) &&
		string.IsNullOrWhiteSpace(TimeIn) &&
		string.IsNullOrWhiteSpace(TimeOut) &&
		string.IsNullOrWhiteSpace(Status) &&
		string.IsNullOrWhiteSpace(Remarks) &&
		string.IsNullOrWhiteSpace(EmployeeName) &&
		string.IsNullOrWhiteSpace(ContactNo);
}
