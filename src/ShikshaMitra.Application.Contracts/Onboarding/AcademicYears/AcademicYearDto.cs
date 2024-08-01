using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Onboarding.AcademicYears;

public class AcademicYearDto : AuditedEntityDto<Guid>
{
    public string Name { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}
