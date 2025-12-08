using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Academic.Curriculum;

/// <summary>
/// DTO for retrieving Subject details
/// </summary>
public class SubjectDto : EntityDto<Guid>
{
	/// <summary>
	/// Subject name
	/// </summary>
	public required string Name { get; set; }

	/// <summary>
	/// Hex color code for visual identification
	/// </summary>
	public required string Color { get; set; }

	/// <summary>
	/// ID of the Education Medium this subject belongs to
	/// </summary>
	public Guid EducationMediumId { get; set; }
}
