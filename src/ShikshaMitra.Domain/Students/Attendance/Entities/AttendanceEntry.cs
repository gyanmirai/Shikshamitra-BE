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
	public Guid? EnrollmentId { get; private set; }
	public DateTime SchoolDate { get; private set; }
	public bool Late { get; private set; }
	public bool HalfDay { get; private set; }
	public double? PresentCredit { get; private set; }
	public double DenominatorCredit { get; private set; } = 1;
	public bool ExcludedFromPercentage { get; private set; }
	public Guid? ApprovedLeaveId { get; private set; }
	public string? PolicySnapshotJson { get; private set; }
	public bool? ExpectedAtSubmission { get; private set; }
	public int? HistoricalRollNo { get; private set; }

	public void SetDailyDetails(Guid enrollmentId, DateTime date, bool late, bool halfDay, double? credit,
		bool excluded, Guid? leaveId, string policyJson, int? historicalRollNo = null, double denominatorCredit = 1)
	{
		if (late && Status != AttendanceStatus.Present || halfDay && Status != AttendanceStatus.Present)
			throw new BusinessException(AttendanceOperationErrorCodes.DetailsRequirePresent);
		Check.NotDefaultOrNull<Guid>(enrollmentId, nameof(enrollmentId));
		if (!double.IsFinite(denominatorCredit) || denominatorCredit is < 0 or > 1 ||
			credit.HasValue && (!double.IsFinite(credit.Value) || credit < 0 || credit > denominatorCredit))
			throw new BusinessException(AttendanceOperationErrorCodes.InvalidCredit);
		EnrollmentId = enrollmentId; SchoolDate = date.Date; Late = late; HalfDay = halfDay;
		PresentCredit = credit; ExcludedFromPercentage = excluded; ApprovedLeaveId = leaveId; PolicySnapshotJson = policyJson;
		ExpectedAtSubmission = true; HistoricalRollNo = historicalRollNo;
		DenominatorCredit = denominatorCredit;
	}

	/// <summary>
	/// Optional remarks about the attendance (e.g., "Late arrival", "Medical leave")
	/// </summary>
	public string? Remarks { get; private set; }
	public string? GuardianAbsenceReason { get; private set; }

	public void SetGuardianAbsenceReason(string reason)
	{
		if (Status != AttendanceStatus.Absent) throw new BusinessException("MobileAcademics:AbsenceRequired");
		GuardianAbsenceReason = Check.Length(Check.NotNullOrWhiteSpace(reason, nameof(reason)).Trim(), nameof(reason), 500);
	}

	/// <summary>
	/// User who marked this attendance
	/// </summary>
	public Guid MarkedByUserId { get; private set; }

	// Navigation properties
	public virtual AttendanceDay AttendanceDay { get; set; } = null!; // Populated by EF relationship materialization.
	public virtual Student Student { get; set; } = null!; // Populated by EF relationship materialization.

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
		ValidateStatus(status);
		Status = status;
		MarkedByUserId = Check.NotDefaultOrNull<Guid>(markedByUserId, nameof(markedByUserId));
		SetRemarks(remarks);
	}

	/// <summary>
	/// Updates the attendance status
	/// </summary>
	public void UpdateStatus(AttendanceStatus status, Guid markedByUserId)
	{
		ValidateStatus(status);
		Status = status;
		MarkedByUserId = markedByUserId;
	}

	private static void ValidateStatus(AttendanceStatus status)
	{
		if (!Enum.IsDefined(status)) throw new BusinessException(AttendanceOperationErrorCodes.InvalidStatus);
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
