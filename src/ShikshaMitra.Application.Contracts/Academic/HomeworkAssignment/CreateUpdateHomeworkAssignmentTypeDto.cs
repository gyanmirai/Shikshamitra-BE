using ShikshaMitra.Constants;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Validation;

namespace ShikshaMitra.Academic.HomeworkAssignment;
public class CreateUpdateHomeworkAssignmentTypeDto
{
	[Required]
	public string Name { get; set; } = string.Empty;

	[Required]
	public WorkType Type { get; set; }
}
