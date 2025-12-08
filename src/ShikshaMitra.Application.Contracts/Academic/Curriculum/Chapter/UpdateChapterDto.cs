using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Academic.Curriculum;

/// <summary>
/// DTO for updating a chapter
/// </summary>
public class UpdateChapterDto
{
	/// <summary>
	/// Chapter number (e.g., 1, 2, 3...)
	/// Must be unique per subject
	/// </summary>
	[Required(ErrorMessage = "Chapter Number is required")]
	[Range(1, 1000, ErrorMessage = "Chapter Number must be between 1 and 1000")]
	public int ChapterNo { get; set; }

	/// <summary>
	/// Chapter name/title
	/// </summary>
	[Required(ErrorMessage = "Chapter Name is required")]
	[StringLength(ChapterConsts.MaxChapterNameLength, MinimumLength = ChapterConsts.MinChapterNameLength, ErrorMessage = "Chapter name must be between {2} and {1} characters")]
	public string ChapterName { get; set; }

	/// <summary>
	/// Chapter description (HTML content)
	/// </summary>
	[StringLength(ChapterConsts.MaxDescriptionLength)]
	public string? Description { get; set; }

	/// <summary>
	/// Chapter start date (must be within academic year)
	/// </summary>
	[Required(ErrorMessage = "Start Date is required")]
	public DateTime StartDate { get; set; }

	/// <summary>
	/// Chapter end date (must be >= start date and within academic year)
	/// </summary>
	[Required(ErrorMessage = "End Date is required")]
	public DateTime EndDate { get; set; }

	/// <summary>
	/// Pre-uploaded document URLs (if using JSON approach)
	/// </summary>
	public List<ChapterDocumentDto>? Documents { get; set; }
}
