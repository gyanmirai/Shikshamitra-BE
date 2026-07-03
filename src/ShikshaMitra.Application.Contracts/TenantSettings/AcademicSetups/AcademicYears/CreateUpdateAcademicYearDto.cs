using System;

namespace ShikshaMitra.TenantSettings.AcademicSetups.AcademicYears;

public class CreateUpdateAcademicYearDto
{
    public string Name { get; set; }
    public DateTime StartYear { get; set; }
    public DateTime EndYear { get; set; }
}
