using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.HR.Policies;

public class PolicyListRequestDto : PagedAndSortedResultRequestDto
{
	public Guid AcademicYearId { get; set; }
	public string? Search { get; set; }
	public string? TargetType { get; set; }
}
