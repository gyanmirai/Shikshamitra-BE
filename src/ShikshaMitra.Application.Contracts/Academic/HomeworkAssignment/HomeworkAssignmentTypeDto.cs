using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public class HomeworkAssignmentTypeDto : AuditedEntityDto<Guid>
{
	public string Name { get; set; } = string.Empty;
	public WorkType Type { get; set; }
	public Guid AcademicMediumId { get; set; }
}
