using System;

namespace ShikshaMitra.TenantSettings.AcademicSetups.AcademicOrganizationOptions;

public class AcademicOrganizationOptionDto
{
	public Guid Id { get; set; }
	public string Label { get; set; } = string.Empty;
	public string StageName { get; set; } = string.Empty;
	public Guid MediumId { get; set; }
	public string MediumName { get; set; } = string.Empty;
	public Guid BoardId { get; set; }
	public string BoardName { get; set; } = string.Empty;
	public Guid AcademicYearId { get; set; }
}
