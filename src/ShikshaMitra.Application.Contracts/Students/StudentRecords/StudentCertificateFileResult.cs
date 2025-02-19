namespace ShikshaMitra.Students.StudentRecords;

public class StudentCertificateFileResult
{
	public byte[] FileContent { get; set; }
	public string FileName { get; set; }
	public string ContentType { get; set; }

	public StudentCertificateFileResult(byte[] fileContent, string fileName, string contentType)
	{
		FileContent = fileContent;
		FileName = fileName;
		ContentType = contentType;
	}
}
