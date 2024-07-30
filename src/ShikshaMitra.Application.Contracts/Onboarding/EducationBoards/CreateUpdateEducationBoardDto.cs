using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Onboarding.EducationBoards;

public class CreateUpdateEducationBoardDto
{
    [Required]
    [StringLength(EducationBoardConsts.MaxNameLength)]
    public string Name { get; set; }
}