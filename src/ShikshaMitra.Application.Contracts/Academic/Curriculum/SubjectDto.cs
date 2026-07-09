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
	public string? DisplayName { get; set; }
	public string? Category { get; set; }

	/// <summary>
	/// Hex color code for visual identification
	/// </summary>
	public required string Color { get; set; }
	public string? Icon { get; set; }
	public string? Description { get; set; }
	public SubjectMasterStatus Status { get; set; }
	public int? SortOrder { get; set; }
	public int UsedInClassesCount { get; set; }
	public int UsedInElectiveGroupsCount { get; set; }
	public bool IsUsed { get; set; }
	public bool CanDelete { get; set; }
	public DateTime? LastUpdatedAt { get; set; }
	public string? LastUpdatedBy { get; set; }

	/// <summary>
	/// ID of the Education Medium this subject belongs to
	/// </summary>
	public Guid EducationMediumId { get; set; }
}
