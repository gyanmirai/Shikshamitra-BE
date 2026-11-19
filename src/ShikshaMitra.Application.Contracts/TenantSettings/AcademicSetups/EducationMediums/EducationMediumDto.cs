using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;

public class EducationMediumDto : AuditedEntityDto<Guid>
{
    public string Name { get; set; } = null!; // The response mapper populates these members after construction.
    public Guid EducationBoardId { get; set; }

    public List<EducationMediumStageDto> EducationMediumStages { get; set; } = null!;
}