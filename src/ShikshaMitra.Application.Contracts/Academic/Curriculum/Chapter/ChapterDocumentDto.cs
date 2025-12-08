using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Academic.Curriculum;

/// <summary>
/// Chapter document reference (for pre-uploaded URLs)
/// </summary>
public class ChapterDocumentDto
{
	/// <summary>
	/// Document file name
	/// </summary>
	[Required(ErrorMessage = "Document name is required")]
	[StringLength(ChapterConsts.MaxFileNameLength)]
	public string Name { get; set; }

	/// <summary>
	/// Document URL (from blob storage/CDN)
	/// </summary>
	[Required(ErrorMessage = "Document URL is required")]
	[Url(ErrorMessage = "Document URL must be valid")]
	[StringLength(ChapterConsts.MaxFileUrlLength)]
	public string Url { get; set; }
}
