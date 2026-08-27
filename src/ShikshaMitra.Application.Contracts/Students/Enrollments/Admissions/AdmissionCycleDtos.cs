using System;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionCycleDto : AuditedEntityDto<Guid>
{
	public string ConcurrencyStamp { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public string Code { get; set; } = string.Empty;
	public Guid AcademicYearId { get; set; }
	public DateTime StartDate { get; set; }
	public DateTime EndDate { get; set; }
	public AdmissionCycleStatus Status { get; set; }
	public DateTime? PublishedAt { get; set; }
}

public class CreateUpdateAdmissionCycleDto
{
	public string? ConcurrencyStamp { get; set; }
	public string Name { get; set; } = string.Empty;
	public string Code { get; set; } = string.Empty;
	public Guid AcademicYearId { get; set; }
	public DateTime StartDate { get; set; }
	public DateTime EndDate { get; set; }
}

public class AdmissionCycleListInput : PagedAndSortedResultRequestDto
{
	public string? Search { get; set; }
	public Guid? AcademicYearId { get; set; }
	public AdmissionCycleStatus? Status { get; set; }
}
