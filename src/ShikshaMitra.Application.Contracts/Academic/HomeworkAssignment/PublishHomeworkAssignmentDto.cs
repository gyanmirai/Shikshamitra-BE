using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Academic.HomeworkAssignment;
public class PublishHomeworkAssignmentDto
{
	[Required]
	public bool IsPublished { get; set; }
}
