using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public class HomeworkStatusUpdateDto
{
    [Required]
    [Range(0, 2)]
    public int Status { get; set; }
}
