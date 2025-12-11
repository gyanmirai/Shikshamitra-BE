using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Academic.HomeworkAssignment;

/// <summary>
/// DTO for AssignmentType entity
/// </summary>
public class AssignmentTypeDto : AuditedEntityDto<Guid>
{
	/// <summary>
	/// Name of the assignment type (e.g., "Group Project", "Case Study")
	/// </summary>
	public string AssignmentTypeName { get; set; } = string.Empty;

	/// <summary>
	/// Associated academic medium ID
	/// </summary>
	public Guid AcademicMediumId { get; set; }
}
