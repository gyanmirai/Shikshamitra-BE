using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.TenantSettings.AcademicSetups.AcademicYears;

public class AcademicYearDto : AuditedEntityDto<Guid>
{
    public string Name { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime StartYear { get; set; }
    public DateTime EndYear { get; set; }
}
