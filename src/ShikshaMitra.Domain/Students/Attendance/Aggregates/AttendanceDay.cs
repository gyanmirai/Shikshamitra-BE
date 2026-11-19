using ShikshaMitra.Security.UserScope;
using System;
using System.Collections.Generic;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.Attendance;

/// <summary>
/// Represents attendance for a specific Standard+Division on a specific date.
/// Header entity to ensure one record per day per division and enable locking.
/// </summary>
public class AttendanceDay : FullAuditedAggregateRoot<Guid>, IMultiTenant, IUserScopedEntity
{
	public Guid? TenantId { get; private set; }

	/// <summary>
	/// Academic year for this attendance day
	/// </summary>
	public Guid AcademicYearId { get; private set; }

	/// <summary>
	/// Standard (Class) for this attendance day
	/// </summary>
	public Guid StandardId { get; private set; }

	/// <summary>
	/// Division for this attendance day
	/// </summary>
	public Guid DivisionId { get; private set; }

	/// <summary>
	/// Date for which attendance is being recorded
	/// </summary>
	public DateTime Date { get; private set; }

	/// <summary>
	/// If true, no further edits are allowed to this day's attendance
	/// </summary>
	public bool IsLocked { get; private set; }
	public bool IsSubmitted { get; private set; }
	public int Revision { get; private set; }
	public Guid? LastOperationId { get; private set; }
	public string? LastOperationHash { get; private set; }
	public string? PolicySnapshotJson { get; private set; }
	public string? SubmittedRosterRevision { get; private set; }

	public void SaveRevision(Guid operationId, string operationHash, bool submit, string rosterRevision, string policyJson)
	{
		Revision++;
		LastOperationId = operationId == Guid.Empty ? null : operationId;
		LastOperationHash = operationHash;
		if (submit)
		{
			IsSubmitted = true;
			SubmittedRosterRevision = rosterRevision;
			PolicySnapshotJson ??= policyJson;
		}
	}

	/// <summary>
	/// User who created/saved this attendance day
	/// </summary>
	public Guid MarkedByUserId { get; private set; }

	// Navigation property
	private readonly List<AttendanceEntry> _entries = new();
	public virtual IReadOnlyCollection<AttendanceEntry> Entries => _entries.AsReadOnly();

	private AttendanceDay()
	{
		/* This constructor is for deserialization / ORM purpose */
	}

	internal AttendanceDay(
		Guid id,
		Guid? tenantId,
		Guid academicYearId,
		Guid standardId,
		Guid divisionId,
		DateTime date,
		Guid markedByUserId)
		: base(id)
	{
		TenantId = tenantId;
		AcademicYearId = Check.NotDefaultOrNull<Guid>(academicYearId, nameof(academicYearId));
		StandardId = Check.NotDefaultOrNull<Guid>(standardId, nameof(standardId));
		DivisionId = Check.NotDefaultOrNull<Guid>(divisionId, nameof(divisionId));
		Date = date.Date; // Ensure we only store date part
		MarkedByUserId = Check.NotDefaultOrNull<Guid>(markedByUserId, nameof(markedByUserId));
		IsLocked = false;
	}

	/// <summary>
	/// Locks this attendance day to prevent further edits
	/// </summary>
	public void Lock()
	{
		if (IsLocked)
		{
			throw new BusinessException("SM:AttendanceDayAlreadyLocked")
				.WithData("Date", Date)
				.WithData("StandardId", StandardId)
				.WithData("DivisionId", DivisionId);
		}

		IsLocked = true;
		Revision++;
	}

	/// <summary>
	/// Unlocks this attendance day to allow edits
	/// </summary>
	public void Unlock()
	{
		IsLocked = false;
		Revision++;
	}

	/// <summary>
	/// Ensures attendance cannot be modified if locked
	/// </summary>
	public void EnsureNotLocked()
	{
		if (IsLocked)
		{
			throw new BusinessException("SM:CannotModifyLockedAttendance")
				.WithData("Date", Date)
				.WithData("StandardId", StandardId)
				.WithData("DivisionId", DivisionId);
		}
	}

	/// <summary>
	/// Updates the user who last modified this attendance day
	/// </summary>
	internal void UpdateMarkedByUser(Guid userId)
	{
		MarkedByUserId = userId;
	}

	#region IUserScopedEntity Implementation
	Guid? IUserScopedEntity.AcademicYearId => AcademicYearId;
	Guid? IUserScopedEntity.EducationBoardId => null;
	Guid? IUserScopedEntity.EducationMediumId => null;
	Guid? IUserScopedEntity.EducationMediumStageId => null;
	Guid? IUserScopedEntity.StandardId => StandardId;
	Guid? IUserScopedEntity.DivisionId => DivisionId;
	#endregion
}
