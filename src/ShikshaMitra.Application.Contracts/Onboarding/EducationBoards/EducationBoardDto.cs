using System;
using System.Collections.Generic;
using ShikshaMitra.Onboarding.EducationalMediums;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Onboarding.EducationBoards;

public class EducationBoardDto : AuditedEntityDto<Guid>
{
    public string Name { get; set; }
    public List<EducationalMediumDto> EducationalMediums { get; set; }
    public List<EducationBoardStageDto> EducationBoardStages { get; set; }
}
