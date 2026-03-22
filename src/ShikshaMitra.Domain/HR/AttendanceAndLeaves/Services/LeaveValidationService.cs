using System;
using System.Threading.Tasks;
using ShikshaMitra.HR;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

/// <summary>
/// Domain service for validating leave requests according to business rules
/// </summary>
public class LeaveValidationService : DomainService
{
	private readonly IEmployeeLeaveRepository _employeeLeaveRepository;
	private readonly ILeavePolicyRepository _leavePolicyRepository;

	public LeaveValidationService(
		IEmployeeLeaveRepository employeeLeaveRepository,
		ILeavePolicyRepository leavePolicyRepository)
	{
		_employeeLeaveRepository = employeeLeaveRepository;
		_leavePolicyRepository = leavePolicyRepository;
	}

	/// <summary>
	/// Validates leave request according to business rules
	/// </summary>
	public async Task ValidateLeaveRequestAsync(
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

		// 2. Check for overlapping leaves
		var hasOverlap = await _employeeLeaveRepository.HasOverlappingLeaveAsync(
			employeeId,
			new DateTimeOffset(startDate),
			new DateTimeOffset(endDate),
			excludeLeaveId);

		if (hasOverlap)
		{
			throw new BusinessException(HRErrorCodes.LeaveOverlap);
		}

		// 3. Get leave policy
		var leavePolicy = await _leavePolicyRepository.FindAsync(x => x.Id == leaveTypeId);
		if (leavePolicy == null)
		{
			throw new BusinessException(HRErrorCodes.LeavePolicyNotFound);
		}

		// 4. Check maximum duration per request (if policy specifies)
		var requestedDays = (endDate - startDate).Days + 1;
		const int maxDaysPerRequest = 30; // Default maximum

		if (requestedDays > maxDaysPerRequest)
		{
			throw new BusinessException(HRErrorCodes.LeaveMaxDaysExceeded);
		}

		// 5. Check annual leave balance
		var currentYear = DateTime.Today.Year;
		var approvedDaysThisYear = await _employeeLeaveRepository.GetApprovedLeaveDaysAsync(
			employeeId,
			leaveTypeId,
			currentYear);

		var remainingBalance = leavePolicy.MaximumDaysPerYear - approvedDaysThisYear;

		if (requestedDays > remainingBalance)
		{
			throw new BusinessException(HRErrorCodes.LeaveInsufficientBalance);
		}

		// 6. Check minimum notice period (2 days advance notice)
		const int minimumNoticeDays = 2;
		var daysDifference = (startDate - DateTime.Today).Days;

		if (daysDifference < minimumNoticeDays)
		{
			throw new BusinessException(HRErrorCodes.LeaveNoticePeriod);
		}

		// 7. Validate against policy requirements
		if (leavePolicy.RequiresApproval && !leavePolicy.IsActive)
		{
			throw new BusinessException(HRErrorCodes.LeavePolicyInactive);
		}
	}

	/// <summary>
	/// Calculates leave days excluding weekends
	/// </summary>
	public int CalculateBusinessDays(DateTime startDate, DateTime endDate)
	{
		var businessDays = 0;
		var currentDate = startDate;

		while (currentDate <= endDate)
		{
			// Exclude weekends (Saturday and Sunday)
			if (currentDate.DayOfWeek != DayOfWeek.Saturday &&
			    currentDate.DayOfWeek != DayOfWeek.Sunday)
			{
				businessDays++;
			}
			currentDate = currentDate.AddDays(1);
		}

		return businessDays;
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
			leaveTypeId,
			currentYear);

		return Math.Max(0, leavePolicy.MaximumDaysPerYear - approvedDays);
	}
}
