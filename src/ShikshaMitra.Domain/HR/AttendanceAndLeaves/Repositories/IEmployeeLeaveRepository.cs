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
	LeavePaidType? PaidType);

public record PayrollApprovedLeaveRow(
	Guid EmployeeId,
	DateTimeOffset StartDate,
	DateTimeOffset EndDate,
	bool HalfDay,
	LeavePaidType? ApprovedPaidType,
	LeavePaidType PolicyPaidType);

public record ApprovedAttendanceLeaveRow(
	Guid LeaveId,
	Guid EmployeeId,
	bool HalfDay,
	DateTimeOffset StartDate,
	DateTimeOffset EndDate);

public record LeaveRuleBalanceUnits(
	Guid LeavePolicyRuleId,
	decimal ApprovedDays,
	decimal PendingDays);

public record LeavePaidTypeBalanceUnits(
	LeavePaidType PaidType,
	decimal ApprovedDays,
	decimal PendingDays);

public record LeaveBalanceUnitRow(
	Guid EmployeeId,
	Guid LeavePolicyId,
	Guid LeavePolicyRuleId,
	LeavePaidType PaidType,
	EmployeeLeaveStatusType Status,
	decimal ApprovedLeaveUnits,
	DateTimeOffset StartDate,
	DateTimeOffset EndDate,
	bool HalfDay,
	Guid? AcademicYearId);

public interface IEmployeeLeaveRepository : IRepository<EmployeeLeave, Guid>
{
	Task<EmployeeLeave?> FindWithoutDetailsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if employee has overlapping leave requests
    /// </summary>
    Task<bool> HasOverlappingLeaveAsync(
        Guid employeeId,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        Guid? excludeLeaveId = null);

	Task<List<LeaveBalanceUnitRow>> GetLeaveBalanceRowsByRuleAsync(
		Guid employeeId,
		IReadOnlyCollection<Guid> leavePolicyRuleIds,
		CancellationToken cancellationToken = default);

	Task<List<LeaveBalanceUnitRow>> GetLeaveBalanceRowsByPolicyAndPaidTypeAsync(
		Guid employeeId,
		Guid leavePolicyId,
		IReadOnlyCollection<LeavePaidType> paidTypes,
		CancellationToken cancellationToken = default);

	Task<IReadOnlySet<Guid>> GetReferencedRuleIdsAsync(
		IReadOnlyCollection<Guid> leavePolicyRuleIds,
		CancellationToken cancellationToken = default);

	Task<List<PayrollApprovedLeaveRow>> GetApprovedPayrollLeaveRowsAsync(
		IReadOnlyCollection<Guid> employeeIds,
		DateTimeOffset rangeStart,
		DateTimeOffset rangeEnd,
		CancellationToken cancellationToken = default);

	Task<List<ApprovedAttendanceLeaveRow>> GetApprovedAttendanceLeaveRowsForDateAsync(
		IReadOnlyCollection<Guid> employeeIds,
		DateOnly attendanceDate,
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
