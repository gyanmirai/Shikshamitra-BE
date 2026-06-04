using System;

namespace ShikshaMitra.HR.Employees;

public class EmployeeDocumentData
{
	public Guid? DocumentNameId { get; set; }
	public string FileName { get; set; } = string.Empty;
	public string ContentType { get; set; } = string.Empty;
	public byte[] Content { get; set; } = [];
	public long Size { get; set; }
}
