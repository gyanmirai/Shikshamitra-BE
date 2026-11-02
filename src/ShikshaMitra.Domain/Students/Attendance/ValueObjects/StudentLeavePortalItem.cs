using System;
using System.Collections.Generic;

namespace ShikshaMitra.Students.Attendance;

/// <summary>Saved portal leave metadata. File bytes remain owned by the attachment/download path.</summary>
public sealed record StudentLeavePortalItem
{
	public Guid Id { get; init; }
	public Guid StudentLeaveTypeId { get; init; }
	public bool IsHalfDay { get; init; }
	public DateTimeOffset LeaveDate { get; init; }
	public DateTimeOffset? EndDate { get; init; }
	public decimal TotalDays { get; init; }
	public string? Description { get; init; }
	public StudentLeaveStatusEnum Status { get; init; }
	public DateTime CreationTime { get; init; }
	public DateTime? LastModificationTime { get; init; }
	public string? RejectionReason { get; init; }
	public string? Remarks { get; init; }
	public string? MobileAttachmentsJson { get; init; }
	public TimeOnly? HalfDayStartTime { get; init; }
	public TimeOnly? HalfDayEndTime { get; init; }
}

public sealed record StudentLeavePortalPage(long TotalCount, List<StudentLeavePortalItem> Items);
public sealed record StudentLeavePortalCalendarItem(Guid Id, StudentLeaveStatusEnum Status, DateTimeOffset LeaveDate, DateTimeOffset? EndDate);
