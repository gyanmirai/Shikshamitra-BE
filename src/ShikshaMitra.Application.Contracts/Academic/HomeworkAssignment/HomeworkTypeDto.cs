using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Academic.HomeworkAssignment;

/// <summary>
/// DTO for HomeworkType entity
/// </summary>
public class HomeworkTypeDto : AuditedEntityDto<Guid>
{
	/// <summary>
	/// Name of the homework type (e.g., "Written Assignment", "Practical Work")
	/// </summary>
	public string HomeworkTypeName { get; set; } = string.Empty;

	/// <summary>
	/// Associated academic medium ID
	/// </summary>
	public Guid AcademicMediumId { get; set; }
}
