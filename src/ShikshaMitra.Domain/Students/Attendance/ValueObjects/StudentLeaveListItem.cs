using System;

namespace ShikshaMitra.Students.Attendance;

/// <summary>Read-only leave page metadata; attachment content and student aggregate graphs are excluded.</summary>
public sealed record StudentLeaveListItem
{
	public Guid Id { get; init; }
	public Guid StudentId { get; init; }
	public string? FirstName { get; init; }
	public string? LastName { get; init; }
	public string? OrganizationName { get; init; }
	public string? ClassName { get; init; }
	public string? DivisionName { get; init; }
	public Guid StudentLeaveTypeId { get; init; }
	public string? LeaveTypeName { get; init; }
	public string? LeaveTypeColor { get; init; }
	public bool IsHalfDay { get; init; }
	public DateTimeOffset LeaveDate { get; init; }
	public DateTimeOffset? EndDate { get; init; }
	public decimal TotalDays { get; init; }
	public string? Description { get; init; }
	public string? Remarks { get; init; }
	public StudentLeaveStatusEnum Status { get; init; }
	public string? RejectionReason { get; init; }
	public bool HasAttachment { get; init; }
	public string? AttachmentFileName { get; init; }
	public DateTime CreationTime { get; init; }
	public Guid? CreatorId { get; init; }
	public DateTime? LastModificationTime { get; init; }
	public Guid? LastModifierId { get; init; }
}
