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
		Guid? excludeLeaveId = null)
	{
		// 1. Validate date range
		if (startDate > endDate)
		{
			throw new BusinessException(HRErrorCodes.LeaveStartAfterEnd);
		}

		if (startDate.Date < DateTime.Today)
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

		// 4. Validate against policy requirements
		if (!leavePolicy.IsActive)
		{
			throw new BusinessException(HRErrorCodes.LeavePolicyInactive);
		}

		// 5. Check maximum duration per request (business days per PRD)
		var requestedDays = LeaveDurationCalculator.CountInclusiveBusinessDays(startDate, endDate);
		if (requestedDays == 0)
		{
			throw new BusinessException(HRErrorCodes.LeaveNoBusinessDaysInRange);
		}
		const int maxDaysPerRequest = 30; // Default maximum

		if (requestedDays > maxDaysPerRequest)
		{
			throw new BusinessException(HRErrorCodes.LeaveMaxDaysExceeded);
		}

		// 6. Check annual leave balance based on the requested leave year.
		// Using server "today" year causes incorrect balance checks for future dated requests.
		var balanceYear = startDate.Year;
		var approvedDaysThisYear = await _employeeLeaveRepository.GetApprovedLeaveDaysAsync(
			employeeId,
			leavePolicy.Id,
			balanceYear);

		var remainingBalance = leavePolicy.LeaveCount - approvedDaysThisYear;

		if (requestedDays > remainingBalance)
		{
			throw new BusinessException(HRErrorCodes.LeaveInsufficientBalance);
		}

		// 7. Check minimum notice period (2 days advance notice)
		const int minimumNoticeDays = 2;
		var daysDifference = (startDate - DateTime.Today).Days;

		if (daysDifference < minimumNoticeDays)
		{
			throw new BusinessException(HRErrorCodes.LeaveNoticePeriod);
		}

		return leavePolicy.Id;
	}

	/// <summary>
	/// Calculates leave days excluding weekends
	/// </summary>
	public int CalculateBusinessDays(DateTime startDate, DateTime endDate)
	{
		return LeaveDurationCalculator.CountInclusiveBusinessDays(startDate, endDate);
	}

	/// <summary>
	/// Gets remaining leave balance for employee
	/// </summary>
	public async Task<int> GetRemainingLeaveBalanceAsync(
		Guid employeeId,
		Guid leaveTypeId)
	{
		var leavePolicy = await _leavePolicyRepository.FindAsync(x => x.Id == leaveTypeId);
		if (leavePolicy == null)
		{
			return 0;
		}

		var currentYear = DateTime.Today.Year;
		var approvedDays = await _employeeLeaveRepository.GetApprovedLeaveDaysAsync(
			employeeId,
			leavePolicy.Id,
			currentYear);

		return Math.Max(0, leavePolicy.LeaveCount - approvedDays);
	}
}
