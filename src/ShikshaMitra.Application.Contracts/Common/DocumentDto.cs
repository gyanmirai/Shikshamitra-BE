namespace ShikshaMitra.Common;

public class DocumentDto : DocumentMetadataDto
{
	public byte[] Content { get; set; } = null!; // The response mapper populates these members after construction.
}