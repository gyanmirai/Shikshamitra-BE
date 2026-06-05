using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.TenantSettings.AcademicSetups.AcademicOrganizationOptions;

public class AcademicOrganizationOptionsInput
{
	[Required]
	public Guid AcademicYearId { get; set; }
}
