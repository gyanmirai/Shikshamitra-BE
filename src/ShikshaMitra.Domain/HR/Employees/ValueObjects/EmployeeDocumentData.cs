using System;

namespace ShikshaMitra.HR.Employees;

public class EmployeeDocumentData
{
	public Guid? DocumentNameId { get; set; }
	public string FileName { get; set; }
	public string ContentType { get; set; }
	public byte[] Content { get; set; }
	public long Size { get; set; }
}