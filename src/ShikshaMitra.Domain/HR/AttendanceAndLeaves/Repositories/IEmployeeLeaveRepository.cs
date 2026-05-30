using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public record LeaveManagementListFilters(
	Guid AcademicYearId,
	EmployeeLeaveStatusType? Status,
	Guid? LeavePolicyId,
	Guid? LeaveTypeMasterId);

public record PayrollApprovedLeaveRow(
	Guid EmployeeId,
	DateTimeOffset StartDate,
	DateTimeOffset EndDate,
	bool HalfDay,
	LeavePaidType? ApprovedPaidType,
	LeavePaidType PolicyPaidType);

public interface IEmployeeLeaveRepository : IRepository<EmployeeLeave, Guid>
{
    /// <summary>
    /// Check if employee has overlapping leave requests
    /// </summary>
    Task<bool> HasOverlappingLeaveAsync(
        Guid employeeId,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        Guid? excludeLeaveId = null);

    /// <summary>
    /// Get total approved leave days for employee in a specific period
    /// </summary>
    Task<decimal> GetApprovedLeaveDaysAsync(
        Guid employeeId,
        Guid leaveTypeId,
        int year);

	/// <summary>
	/// Get total approved leave days for employee for a leave policy.
	/// </summary>
	Task<decimal> GetApprovedLeaveDaysAsync(
		Guid employeeId,
		Guid leaveTypeId);

	Task<List<PayrollApprovedLeaveRow>> GetApprovedPayrollLeaveRowsAsync(
		IReadOnlyCollection<Guid> employeeIds,
		DateTimeOffset rangeStart,
		DateTimeOffset rangeEnd,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Paged leave ids for management list: start date in [rangeStart, rangeEnd] (date inclusive), policy academic year, optional filters.
	/// </summary>
	Task<(int TotalCount, List<Guid> PageIds)> GetPagedIdsForManagementListAsync(
		DateTime rangeStart,
		DateTime rangeEnd,
		LeaveManagementListFilters filters,
		string? search,
		int skip,
		int take,
		Guid? employeeId = null,
		IReadOnlyCollection<Guid>? employeeIds = null,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Paged leave ids overlapping [rangeStart, rangeEnd] for calendar views.
	/// </summary>
	Task<(int TotalCount, List<Guid> PageIds)> GetPagedIdsForCalendarAsync(
		DateTimeOffset rangeStart,
		DateTimeOffset rangeEnd,
		LeaveManagementListFilters filters,
		string? search,
		int skip,
		int take,
		Guid? employeeId = null,
		IReadOnlyCollection<Guid>? employeeIds = null,
		CancellationToken cancellationToken = default);
}
