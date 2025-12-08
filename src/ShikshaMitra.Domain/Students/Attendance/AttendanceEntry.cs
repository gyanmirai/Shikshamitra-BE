using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.Attendance;

/// <summary>
/// Represents a single student's attendance entry for a specific day.
/// Links an enrollment to an attendance day with status and remarks.
/// </summary>
public class AttendanceEntry : FullAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }

	/// <summary>
	/// Foreign key to AttendanceDay
	/// </summary>
	public Guid AttendanceDayId { get; private set; }

	/// <summary>
	/// Foreign key to Student
	/// </summary>
	public Guid StudentId { get; private set; }

	/// <summary>
	/// Attendance status for this student on this day
	/// </summary>
	public AttendanceStatus Status { get; private set; }

	/// <summary>
	/// Optional remarks about the attendance (e.g., "Late arrival", "Medical leave")
	/// </summary>
	public string? Remarks { get; private set; }

	/// <summary>
	/// User who marked this attendance
	/// </summary>
	public Guid MarkedByUserId { get; private set; }

	// Navigation properties
	public virtual AttendanceDay AttendanceDay { get; set; }
	public virtual Student Student { get; set; }

	private AttendanceEntry()
	{
		/* This constructor is for deserialization / ORM purpose */
	}

	internal AttendanceEntry(
		Guid id,
		Guid? tenantId,
		Guid attendanceDayId,
		Guid studentId,
		AttendanceStatus status,
		Guid markedByUserId,
		string? remarks = null)
		: base(id)
	{
		TenantId = tenantId;
		AttendanceDayId = Check.NotDefaultOrNull<Guid>(attendanceDayId, nameof(attendanceDayId));
		StudentId = Check.NotDefaultOrNull<Guid>(studentId, nameof(studentId));
		Status = status;
		MarkedByUserId = Check.NotDefaultOrNull<Guid>(markedByUserId, nameof(markedByUserId));
		SetRemarks(remarks);
	}

	/// <summary>
	/// Updates the attendance status
	/// </summary>
	public void UpdateStatus(AttendanceStatus status, Guid markedByUserId)
	{
		Status = status;
		MarkedByUserId = markedByUserId;
	}

	/// <summary>
	/// Updates remarks with validation
	/// </summary>
	public void SetRemarks(string? remarks)
	{
		if (!string.IsNullOrWhiteSpace(remarks) && remarks.Length > 500)
		{
			throw new BusinessException("SM:RemarksExceedsMaxLength")
				.WithData("MaxLength", 500)
				.WithData("ActualLength", remarks.Length);
		}

		Remarks = remarks;
	}
}
