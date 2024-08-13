using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.TenantSettings.EducationBoards;

public class CreateUpdateEducationBoardDto
{
    [Required]
    [StringLength(EducationBoardConsts.MaxNameLength)]
    public string Name { get; set; }
    
    public Guid AcademicYearId { get; set; }
}