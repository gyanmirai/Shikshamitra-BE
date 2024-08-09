using System;
using System.Collections.Generic;
using ShikshaMitra.Onboarding.Divisions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Onboarding.Standards;

public class StandardDto : AuditedEntityDto<Guid>
{
    public string Name { get; set; }

    public Guid AcademicYearId { get; set; }
    public Guid EducationBoardId { get; set; }
    public Guid EducationMediumId { get; set; }
    public Guid EducationMediumStageId { get; set; }
}