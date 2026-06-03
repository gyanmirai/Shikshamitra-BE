using System;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.HR;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class LeaveValidationService : DomainService
{
	private readonly IEmployeeLeaveRepository _employeeLeaveRepository;
	private readonly ILeavePolicyRepository _leavePolicyRepository;
	private readonly LeaveReportingChainService _reportingChainService;

	public LeaveValidationService(
		IEmployeeLeaveRepository employeeLeaveRepository,
		ILeavePolicyRepository leavePolicyRepository,
		LeaveReportingChainService reportingChainService)
	{
		_employeeLeaveRepository = employeeLeaveRepository;
		_leavePolicyRepository = leavePolicyRepository;
		_reportingChainService = reportingChainService;
	}

	public async Task EnsureActorMayChangeLeaveStatusAsync(
		Guid leaveRequesterEmployeeId,
		Guid actorEmployeeId,
		CancellationToken cancellationToken = default)
	{
		await _reportingChainService.EnsureActorMayChangeLeaveStatusAsync(
			leaveRequesterEmployeeId,
			actorEmployeeId,
			cancellationToken);
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
		if (startDate > endDate)
		{
			throw new BusinessException(HRErrorCodes.LeaveStartAfterEnd);
		}

		EnsureValidHalfDayRange(startDate, endDate, halfDay);

		var today = Clock.Now.Date;
		if (enforceEmployeeTimingRules && startDate.Date < today)
		{
			throw new BusinessException(HRErrorCodes.LeavePastDate);
		}

		var hasOverlap = await _employeeLeaveRepository.HasOverlappingLeaveAsync(
			employeeId,
			new DateTimeOffset(startDate),
			new DateTimeOffset(endDate),
			excludeLeaveId);

		if (hasOverlap)
		{
			throw new BusinessException(HRErrorCodes.LeaveOverlap);
		}

		var leavePolicy = await _leavePolicyRepository.FindAsync(x => x.Id == leaveTypeId);
		if (leavePolicy == null)
		{
			throw new BusinessException(HRErrorCodes.LeavePolicyNotFound);
		}

		if (academicYearId.HasValue && leavePolicy.AcademicYearId != academicYearId.Value)
		{
			throw new BusinessException(HRErrorCodes.LeavePolicyNotFound);
		}

		if (!leavePolicy.IsActive)
		{
			throw new BusinessException(HRErrorCodes.LeavePolicyInactive);
		}

		var requestedUnits = LeaveDurationCalculator.CountLeaveUnits(startDate, endDate, halfDay);
		if (requestedUnits <= 0)
		{
			throw new BusinessException(HRErrorCodes.LeaveNoBusinessDaysInRange);
		}
		const int maxDaysPerRequest = 30;

		if (requestedUnits > maxDaysPerRequest)
		{
			throw new BusinessException(HRErrorCodes.LeaveMaxDaysExceeded);
		}

		// 6. Check leave balance for the selected leave policy.
		// Leave policies are academic-year scoped, so policy-level approved days are used.
		await EnsureSufficientLeaveBalanceAsync(employeeId, leavePolicy, requestedUnits);

		const int minimumNoticeDays = 2;
		var daysDifference = (startDate.Date - today).Days;

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

	public decimal CalculateBusinessDays(DateTime startDate, DateTime endDate)
	{
		return LeaveDurationCalculator.CountInclusiveBusinessDays(startDate, endDate);
	}

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
