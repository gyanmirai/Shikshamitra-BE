using System;
using System.Collections.Generic;
using ShikshaMitra.TenantSettings.EducationBoards;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.TenantSettings.EducationMediums;

public class EducationMediumDto : AuditedEntityDto<Guid>
{
    public string Name { get; set; }
    public Guid EducationBoardId { get; set; }

    public List<EducationMediumStageDto> EducationMediumStages { get; set; }
}