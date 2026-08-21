using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionCampusDto : FullAuditedEntityDto<Guid>
{
	public string Code { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public bool IsActive { get; set; }
}

public class AdmissionCampusListInput : PagedAndSortedResultRequestDto
{
	public string? Search { get; set; }
	public bool? IsActive { get; set; }
}

public class CreateUpdateAdmissionCampusDto
{
	public string Code { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
}
