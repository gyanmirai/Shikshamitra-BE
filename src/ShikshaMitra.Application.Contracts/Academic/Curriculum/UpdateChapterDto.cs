using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShikshaMitra.Academic.Curriculum;
public class UpdateChapterDto
{

	[Range(1, int.MaxValue, ErrorMessage = "Chapter number must be greater than 0.")]
	public int ChapterNumber { get; set; }

	[Required]
	[StringLength(200, ErrorMessage = "Chapter name cannot exceed 200 characters.")]
	public string Name { get; set; } = default!;

	[Required]
	public DateTimeOffset StartDate { get; set; }

	[Required]
	public DateTimeOffset EndDate { get; set; }

	[StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters.")]
	public string? Description { get; set; }

	public List<ChapterDocumentDto>? Documents { get; set; }
}
