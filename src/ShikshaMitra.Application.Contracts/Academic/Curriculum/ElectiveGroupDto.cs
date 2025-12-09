using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Academic.Curriculum;

/// <summary>
/// DTO for retrieving elective group details with associated subjects
/// </summary>
public class ElectiveGroupDto : EntityDto<Guid>
{
	/// <summary>
	/// Name of the elective group
	/// </summary>
	public required string Name { get; set; }

	/// <summary>
	/// Hex color code for UI display (e.g., #FF5722)
	/// </summary>
	public required string Color { get; set; }

	/// <summary>
	/// Education medium this elective group belongs to
	/// </summary>
	public Guid EducationMediumId { get; set; }

	/// <summary>
	/// List of subjects included in this elective group
	/// </summary>
	public List<SubjectDto> Subjects { get; set; } = new List<SubjectDto>();
}
