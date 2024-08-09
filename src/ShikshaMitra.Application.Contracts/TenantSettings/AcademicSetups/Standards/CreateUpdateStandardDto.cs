using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Onboarding.Standards;

public class CreateUpdateStandardDto
{
    [Required]
    [StringLength(StandardConsts.MaxNameLength)]
    public string Name { get; set; }

    public Guid AcademicYearId { get; set; }
    public Guid EducationBoardId { get; set; }
    public Guid EducationMediumId { get; set; }
    public Guid EducationMediumStageId { get; set; }
}