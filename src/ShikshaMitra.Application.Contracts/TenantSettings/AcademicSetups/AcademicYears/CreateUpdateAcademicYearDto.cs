using System;

namespace ShikshaMitra.TenantSettings.AcademicSetups.AcademicYears;

public class CreateUpdateAcademicYearDto
{
    public string Name { get; set; } = null!; // Request binding defers initialization; retain the null default and existing validation.
    public DateTime StartYear { get; set; }
    public DateTime EndYear { get; set; }
}
