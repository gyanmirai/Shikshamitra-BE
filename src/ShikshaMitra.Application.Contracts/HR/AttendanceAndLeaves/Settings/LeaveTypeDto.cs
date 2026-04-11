using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

/// <summary>
/// DTO for leave type
/// </summary>
public class LeaveTypeDto : FullAuditedEntityDto<Guid>
{
	/// <summary>
	/// Type/Name of the leave
	/// </summary>
	public string Type { get; set; } = string.Empty;

	/// <summary>
	/// Color code for UI display (HEX format)
	/// </summary>
	public string Color { get; set; } = string.Empty;

	/// <summary>
	/// Whether this leave type is active
	/// </summary>
	public bool IsActive { get; set; }
}
