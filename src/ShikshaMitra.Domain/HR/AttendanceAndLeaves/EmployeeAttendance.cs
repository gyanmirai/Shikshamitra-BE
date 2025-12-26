using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.Security.UserScope;
using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Employees;

public class EmployeeAttendance : FullAuditedEntity<Guid>, IMultiTenant, IUserScopedEntity
{
	public Guid? TenantId { get; set; }
	public Guid EmployeeId { get; set; }

	/// <summary>
	/// The date of attendance (no time component).
	/// Used for unique constraint (TenantId, EmployeeId, AttendanceDate) and date-based queries.
	/// </summary>
	public DateOnly AttendanceDate { get; set; }

	// Store only TimeIn and TimeOut as DateTimeOffset values (nullable for ABSENT/PENDING days)
	public DateTimeOffset? TimeIn { get; set; }
	public DateTimeOffset? TimeOut { get; set; }

	public EmployeeAttendanceStatusType StatusType { get; set; }

	// Concrete FK for scope filtering - denormalized from Employee
	// This ensures EF Core can apply scope filters without relying on navigation properties
	// Nullable to support existing records created before this column was added
	public Guid? EducationMediumStageId { get; set; }

	public double? TotalHoursWorked
	{
		get
		{
			switch (StatusType)
			{
				case EmployeeAttendanceStatusType.FullDayLeave:
					return 0.0;
				case EmployeeAttendanceStatusType.HalfDayLeave:
					return 4.0;
				case EmployeeAttendanceStatusType.Present:
					if (!TimeIn.HasValue || !TimeOut.HasValue)
					{
						return null;
					}

					var duration = TimeOut.Value - TimeIn.Value;
					return Math.Round(duration.TotalHours, 2);

				default:
					return null;
			}
		}
	}

	public virtual Employee Employee { get; set; }

	#region IUserScopedEntity Explicit Implementation
	// Use concrete FK properties instead of navigating through nullable Employee
	// Employee attendance is not filtered by academic year/board/medium/standard/division
	Guid? IUserScopedEntity.AcademicYearId => null;
	Guid? IUserScopedEntity.EducationBoardId => null;
	Guid? IUserScopedEntity.EducationMediumId => null;
	Guid? IUserScopedEntity.EducationMediumStageId => EducationMediumStageId;
	Guid? IUserScopedEntity.StandardId => null;
	Guid? IUserScopedEntity.DivisionId => null;
	#endregion
}