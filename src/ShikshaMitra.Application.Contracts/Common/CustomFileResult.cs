namespace ShikshaMitra.Common;

public class CustomFileResult
{
	public byte[] FileContent { get; set; }
	public string FileName { get; set; }
	public string ContentType { get; set; }

	public CustomFileResult(byte[] fileContent, string fileName, string contentType)
	{
		FileContent = fileContent;
		FileName = fileName;
		ContentType = contentType;
	}
}
