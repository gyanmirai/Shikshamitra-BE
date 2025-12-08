namespace ShikshaMitra.Academic.Curriculum.SubjectAssignment;

/// <summary>
/// Document attachment for subject assignment
/// </summary>
public class SubjectDocumentDto
{
	/// <summary>
	/// Document name/title (e.g., "syllabus.pdf")
	/// </summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// Document URL (CDN or blob storage)
	/// </summary>
	public string Url { get; set; } = string.Empty;
}
