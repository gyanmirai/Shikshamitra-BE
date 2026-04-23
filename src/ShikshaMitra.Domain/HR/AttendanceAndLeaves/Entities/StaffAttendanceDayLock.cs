using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;
using ShikshaMitra.Security.UserScope;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

/// <summary>
/// Prevents further manual edits for staff attendance on a given calendar date.
/// </summary>
public class StaffAttendanceDayLock : AuditedAggregateRoot<Guid>, IMultiTenant, IUserScopedEntity
{
	public Guid? TenantId { get; private set; }

	public DateOnly LockDate { get; private set; }

	public Guid LockedByUserId { get; private set; }

	public DateTime LockedAt { get; private set; }

	private StaffAttendanceDayLock() { }

	public StaffAttendanceDayLock(
		Guid id,
		Guid? tenantId,
		DateOnly lockDate,
		Guid lockedByUserId,
		DateTime lockedAt) : base(id)
	{
		TenantId = tenantId;
		LockDate = lockDate;
		LockedByUserId = lockedByUserId;
		LockedAt = lockedAt;
	}

	Guid? IUserScopedEntity.AcademicYearId => null;
	Guid? IUserScopedEntity.EducationBoardId => null;
	Guid? IUserScopedEntity.EducationMediumId => null;
	Guid? IUserScopedEntity.EducationMediumStageId => null;
	Guid? IUserScopedEntity.StandardId => null;
	Guid? IUserScopedEntity.DivisionId => null;
}
