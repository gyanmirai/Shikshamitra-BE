using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.HR;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Employees;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

/// <summary>
/// Domain service for validating leave requests according to business rules
/// </summary>
public class LeaveValidationService : DomainService
{
	private const int MaxReportingHierarchyDepth = 64;

	private readonly IEmployeeLeaveRepository _employeeLeaveRepository;
	private readonly ILeavePolicyRepository _leavePolicyRepository;
	private readonly IEmployeeRepository _employeeRepository;

	public LeaveValidationService(
		IEmployeeLeaveRepository employeeLeaveRepository,
		ILeavePolicyRepository leavePolicyRepository,
		IEmployeeRepository employeeRepository)
	{
		_employeeLeaveRepository = employeeLeaveRepository;
		_leavePolicyRepository = leavePolicyRepository;
		_employeeRepository = employeeRepository;
	}

	/// <summary>
	/// Ensures <paramref name="actorEmployeeId"/> is in the leave requester's reporting-manager chain
	/// (direct manager or any ancestor manager), and is not the requester.
	/// </summary>
	public async Task EnsureActorMayChangeLeaveStatusAsync(
		Guid leaveRequesterEmployeeId,
		Guid actorEmployeeId,
		CancellationToken cancellationToken = default)
	{
		if (leaveRequesterEmployeeId == actorEmployeeId)
		{
			throw new BusinessException(HRErrorCodes.LeaveApproverNotInReportingChain);
		}

		var approverIds = await BuildApproverEmployeeIdSetAsync(leaveRequesterEmployeeId, cancellationToken);
		if (!approverIds.Contains(actorEmployeeId))
		{
			throw new BusinessException(HRErrorCodes.LeaveApproverNotInReportingChain);
		}
	}

	private async Task<HashSet<Guid>> BuildApproverEmployeeIdSetAsync(
		Guid requesterEmployeeId,
		CancellationToken cancellationToken)
	{
		var approvers = new HashSet<Guid>();
		var requester = await _employeeRepository.FindAsync(requesterEmployeeId, includeDetails: false, cancellationToken);
		if (requester == null)
		{
			return approvers;
		}

		Guid? walker = requester.ReportingManagerId;
		var visited = new HashSet<Guid> { requesterEmployeeId };
		var depth = 0;

		while (walker != null && depth < MaxReportingHierarchyDepth)
		{
			depth++;
			if (!visited.Add(walker.Value))
			{
				throw new BusinessException(HRErrorCodes.LeaveReportingManagerCycle);
			}

			approvers.Add(walker.Value);
			var next = await _employeeRepository.FindAsync(walker.Value, includeDetails: false, cancellationToken);
			if (next == null)
			{
				break;
			}

			walker = next.ReportingManagerId;
		}

		if (depth >= MaxReportingHierarchyDepth && walker != null)
		{
			throw new BusinessException(HRErrorCodes.LeaveReportingManagerCycle);
		}

		return approvers;
	}

	/// <summary>
	/// Validates leave request according to business rules.
	/// Returns the resolved LeavePolicy ID so callers can pass the correct FK.
	/// </summary>
	public async Task<Guid> ValidateLeaveRequestAsync(
		Guid employeeId,
		Guid leaveTypeId,
		DateTime startDate,
		DateTime endDate,
		bool halfDay = false,
		Guid? academicYearId = null,
		bool enforceEmployeeTimingRules = true,
		Guid? excludeLeaveId = null)
	{
		// 1. Validate date range
		if (startDate > endDate)
		{
			throw new BusinessException(HRErrorCodes.LeaveStartAfterEnd);
		}

		EnsureValidHalfDayRange(startDate, endDate, halfDay);

		if (enforceEmployeeTimingRules && startDate.Date < DateTime.Today)
		{
			throw new BusinessException(HRErrorCodes.LeavePastDate);
		}

		// 2. Check for overlapping leaves (exclude Cancelled leaves handled by repo)
		var hasOverlap = await _employeeLeaveRepository.HasOverlappingLeaveAsync(
			employeeId,
			new DateTimeOffset(startDate),
			new DateTimeOffset(endDate),
			excludeLeaveId);

		if (hasOverlap)
		{
			throw new BusinessException(HRErrorCodes.LeaveOverlap);
		}

		// 3. Get leave policy (leaveTypeId is the LeavePolicy PK)
		var leavePolicy = await _leavePolicyRepository.FindAsync(x => x.Id == leaveTypeId);
		if (leavePolicy == null)
		{
			throw new BusinessException(HRErrorCodes.LeavePolicyNotFound);
		}

		if (academicYearId.HasValue && leavePolicy.AcademicYearId != academicYearId.Value)
		{
			throw new BusinessException(HRErrorCodes.LeavePolicyNotFound);
		}

		// 4. Validate against policy requirements
		if (!leavePolicy.IsActive)
		{
			throw new BusinessException(HRErrorCodes.LeavePolicyInactive);
		}

		// 5. Check maximum duration per request (Sunday is the weekly off day)
		var requestedUnits = LeaveDurationCalculator.CountLeaveUnits(startDate, endDate, halfDay);
		if (requestedUnits <= 0)
		{
			throw new BusinessException(HRErrorCodes.LeaveNoBusinessDaysInRange);
		}
		const int maxDaysPerRequest = 30; // Default maximum

		if (requestedUnits > maxDaysPerRequest)
		{
			throw new BusinessException(HRErrorCodes.LeaveMaxDaysExceeded);
		}

		// 6. Check leave balance for the selected leave policy.
		// Leave policies are academic-year scoped, so policy-level approved days are used.
		await EnsureSufficientLeaveBalanceAsync(employeeId, leavePolicy, requestedUnits);

		// 7. Check minimum notice period (2 days advance notice)
		const int minimumNoticeDays = 2;
		var daysDifference = (startDate - DateTime.Today).Days;

		if (enforceEmployeeTimingRules && daysDifference < minimumNoticeDays)
		{
			throw new BusinessException(HRErrorCodes.LeaveNoticePeriod);
		}

		return leavePolicy.Id;
	}

	public async Task EnsureSufficientLeaveBalanceAsync(
		Guid employeeId,
		Guid leavePolicyId,
		DateTime startDate,
		DateTime endDate,
		bool halfDay = false)
	{
		var leavePolicy = await _leavePolicyRepository.FindAsync(x => x.Id == leavePolicyId);
		if (leavePolicy == null)
		{
			throw new BusinessException(HRErrorCodes.LeavePolicyNotFound);
		}

		EnsureValidHalfDayRange(startDate, endDate, halfDay);

		var requestedUnits = LeaveDurationCalculator.CountLeaveUnits(startDate, endDate, halfDay);
		if (requestedUnits <= 0)
		{
			throw new BusinessException(HRErrorCodes.LeaveNoBusinessDaysInRange);
		}

		await EnsureSufficientLeaveBalanceAsync(employeeId, leavePolicy, requestedUnits);
	}

	/// <summary>
	/// Calculates leave days excluding Sundays
	/// </summary>
	public decimal CalculateBusinessDays(DateTime startDate, DateTime endDate)
	{
		return LeaveDurationCalculator.CountInclusiveBusinessDays(startDate, endDate);
	}

	/// <summary>
	/// Gets remaining leave balance for employee
	/// </summary>
	public async Task<decimal> GetRemainingLeaveBalanceAsync(
		Guid employeeId,
		Guid leaveTypeId)
	{
		var leavePolicy = await _leavePolicyRepository.FindAsync(x => x.Id == leaveTypeId);
		if (leavePolicy == null)
		{
			return 0;
		}

		var approvedDays = await _employeeLeaveRepository.GetApprovedLeaveDaysAsync(
			employeeId,
			leavePolicy.Id);

		return Math.Max(0, leavePolicy.LeaveCount - approvedDays);
	}

	private async Task EnsureSufficientLeaveBalanceAsync(Guid employeeId, LeavePolicy leavePolicy, decimal requestedUnits)
	{
		var approvedDaysForPolicy = await _employeeLeaveRepository.GetApprovedLeaveDaysAsync(
			employeeId,
			leavePolicy.Id);

		var remainingBalance = leavePolicy.LeaveCount - approvedDaysForPolicy;

		if (requestedUnits > remainingBalance)
		{
			throw new BusinessException(HRErrorCodes.LeaveInsufficientBalance);
		}
	}

	private static void EnsureValidHalfDayRange(DateTime startDate, DateTime endDate, bool halfDay)
	{
		if (halfDay && startDate.Date != endDate.Date)
		{
			throw new BusinessException(HRErrorCodes.LeaveHalfDayMustBeSingleDate);
		}
	}
}
