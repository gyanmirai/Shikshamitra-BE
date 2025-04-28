using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.StudentRecords;

public class AutoAssignStudentsToGroupsDto
{
    [Required]
    public Guid EducationMediumId { get; set; }
    
    [Required]
    public List<Guid> GroupIds { get; set; }
    
    public Guid? DivisionId { get; set; }
}
