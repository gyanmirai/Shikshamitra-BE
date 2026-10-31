using System;
using System.Collections.Generic;

namespace ShikshaMitra.Academic.Curriculum;

/// <summary>
/// Response DTO for chapter
/// Aligned with FE contract
/// </summary>
public class ChapterResponseDto
{
	/// <summary>
	/// Chapter ID
	/// </summary>
	public Guid Id { get; set; }

	/// <summary>
	/// Chapter number
	/// </summary>
	public int ChapterNo { get; set; }

	/// <summary>
	/// Chapter name/title
	/// </summary>
	public string? ChapterName { get; set; }

	/// <summary>
	/// Chapter description (HTML content)
	/// </summary>
	public string? Description { get; set; }

	/// <summary>
	/// Chapter start date (ISO 8601 format)
	/// </summary>
	public DateTime StartDate { get; set; }

	/// <summary>
	/// Chapter end date (ISO 8601 format)
	/// </summary>
	public DateTime EndDate { get; set; }

	/// <summary>
	/// List of documents attached to this chapter
	/// </summary>
	public List<ChapterDocumentDto> Documents { get; set; } = new();

	/// <summary>
	/// Academic medium ID
	/// </summary>
	public Guid AcademicMediumId { get; set; }

	/// <summary>
	/// Class/Standard ID
	/// </summary>
	public Guid ClassId { get; set; }

	/// <summary>
	/// Subject ID
	/// </summary>
	public Guid SubjectId { get; set; }

	/// <summary>
	/// When this chapter was created
	/// </summary>
	public DateTime CreatedAt { get; set; }
}
