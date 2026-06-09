using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

/// <summary>
/// DTO for leave policy
/// </summary>
public class LeavePolicyDto : FullAuditedEntityDto<Guid>
{
	public string PolicyName { get; set; } = string.Empty;
	public string? Description { get; set; }
	public string Status { get; set; } = string.Empty;
	public DateTime EffectiveFrom { get; set; }
	public DateTime? EffectiveTo { get; set; }
	public int RuleCount { get; set; }
	public bool IsActive { get; set; }
}
