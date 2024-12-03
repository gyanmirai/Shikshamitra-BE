namespace ShikshaMitra.HR.Employees;

public class DocumentDto
{
	public string FileName { get; set; }
	public string ContentType { get; set; }
	public byte[] Content { get; set; }
	public long Size { get; set; }
}