using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.HR.Policies;

public class PolicyListRequestDto : PagedAndSortedResultRequestDto
{
	public Guid AcademicYearId { get; set; }
	public string? Search { get; set; }
	public string? TargetType { get; set; }
	public bool? IsArchived { get; set; }
	public bool? IsActive { get; set; }
	public Guid? PayrollPolicyId { get; set; }
	public Guid? AttendancePolicyId { get; set; }
	public Guid? LeavePolicyId { get; set; }
	public DateTime? ActiveOnDate { get; set; }
	public DateTime? EffectiveFrom { get; set; }
	public DateTime? EffectiveTo { get; set; }
}
