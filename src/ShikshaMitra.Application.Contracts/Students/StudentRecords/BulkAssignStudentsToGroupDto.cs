using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.StudentRecords;

public class BulkAssignStudentsToGroupDto
{
    [Required]
    public List<Guid> StudentIds { get; set; }

    [Required]
    public Guid StudentGroupId { get; set; }
}