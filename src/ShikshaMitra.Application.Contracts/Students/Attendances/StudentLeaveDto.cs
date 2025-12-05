using System;
using ShikshaMitra.Students.Attendances;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.Attendances;

/// <summary>
/// DTO for StudentLeave entity - used for output
/// </summary>
public class StudentLeaveDto : FullAuditedEntityDto<Guid>
{
	public Guid StudentId { get; set; }
	public string StudentName { get; set; } = string.Empty;
	public string? OrganizationName { get; set; }
	public string? ClassName { get; set; }
	public string? DivisionName { get; set; }
	public Guid StudentLeaveTypeId { get; set; }
	public string LeaveTypeName { get; set; } = string.Empty;
	public string LeaveTypeColor { get; set; } = string.Empty;
	public bool IsHalfDay { get; set; }
	public DateTimeOffset LeaveDate { get; set; }
	public DateTimeOffset? EndDate { get; set; }
	public decimal TotalDays { get; set; }
	public string? Description { get; set; }
	public string? Remarks { get; set; }
	public StudentLeaveStatusEnum Status { get; set; }
	public string? RejectionReason { get; set; }
	public bool HasAttachment { get; set; }
	public string? AttachmentFileName { get; set; }
}
