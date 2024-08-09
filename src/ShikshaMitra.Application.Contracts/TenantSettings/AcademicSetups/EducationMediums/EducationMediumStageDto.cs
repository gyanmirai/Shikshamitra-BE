using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Onboarding.EducationBoards;

public class EducationMediumStageDto : AuditedEntityDto<Guid>
{
    public Guid EducationMediumId { get; set; }
    public string Name { get; set; }
}
