using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Academic.Curriculum;

public class BulkSubmitLessonPlansDto
{
    [Required]
    public List<Guid> LessonPlanIds { get; set; } = new();
}
