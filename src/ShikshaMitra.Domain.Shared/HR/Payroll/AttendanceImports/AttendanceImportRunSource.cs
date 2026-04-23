namespace ShikshaMitra.HR.Payroll.AttendanceImports;

/// <summary>
/// Origin of an attendance import batch (file upload vs integration vs internal loader).
/// </summary>
public enum AttendanceImportRunSource
{
	FileUpload = 0,
	Integration = 1,
	Internal = 2
}