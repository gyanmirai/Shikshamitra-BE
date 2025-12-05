using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.Attendances.Settings;

/// <summary>
/// DTO for displaying student leave type information
/// </summary>
public class StudentLeaveTypeDto : FullAuditedEntityDto<Guid>
{
	/// <summary>
	/// Name of the leave type
	/// </summary>
	public string LeaveTypeName { get; set; } = string.Empty;

	/// <summary>
	/// Color code for UI representation
	/// </summary>
	public string Color { get; set; } = string.Empty;

	/// <summary>
	/// Indicates if this leave type is active
	/// </summary>
	public bool IsActive { get; set; }
}
