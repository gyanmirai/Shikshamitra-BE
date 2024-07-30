using System;
using System.Collections.Generic;
using ShikshaMitra.Onboarding.Standards;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Onboarding.EducationalMediums;

public class EducationalMediumDto : AuditedEntityDto<Guid>
{
    public string Name { get; set; }
    public Guid EducationBoardId { get; set; }
    public List<StandardDto> Standards { get; set; }
}