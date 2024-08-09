using ShikshaMitra.Onboarding.EducationBoards;
using System;
using System.Collections.Generic;
using ShikshaMitra.Onboarding.Standards;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Onboarding.EducationMediums;

public class EducationMediumDto : AuditedEntityDto<Guid>
{
    public string Name { get; set; }
    public Guid EducationBoardId { get; set; }
    public List<StandardDto> Standards { get; set; }
    public List<EducationMediumStageDto> EducationMediumStages { get; set; }
}