using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Policies;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class LeaveValidationService : DomainService
{
	private readonly IEmployeeLeaveRepository _employeeLeaveRepository;
	private readonly IRepository<LeavePolicyRule, Guid> _leavePolicyRuleRepository;
	private readonly LeaveReportingChainService _reportingChainService;
	private readonly LeaveWorkCalendarUnitService _leaveWorkCalendarUnitService;
	private readonly HrPolicyResolver _hrPolicyResolver;

	public LeaveValidationService(
		IEmployeeLeaveRepository employeeLeaveRepository,
		IRepository<LeavePolicyRule, Guid> leavePolicyRuleRepository,
		LeaveReportingChainService reportingChainService,
		LeaveWorkCalendarUnitService leaveWorkCalendarUnitService,
		HrPolicyResolver hrPolicyResolver)
	{
		_employeeLeaveRepository = employeeLeaveRepository;
		_leavePolicyRuleRepository = leavePolicyRuleRepository;
		_reportingChainService = reportingChainService;
		_leaveWorkCalendarUnitService = leaveWorkCalendarUnitService;
		_hrPolicyResolver = hrPolicyResolver;
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

	public async Task<LeaveRequestPolicyResolution> ValidateLeaveRequestAsync(
		Guid employeeId,
		LeavePaidType paidType,
		DateTime startDate,
		DateTime endDate,
		bool halfDay = false,
		Guid? academicYearId = null,
		bool enforceEmployeeTimingRules = true,
		Guid? excludeLeaveId = null,
		bool hasAttachment = false)
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

		var resolvedPolicies = await _hrPolicyResolver.ResolveAsync(employeeId, startDate, academicYearId);
		var leavePolicy = resolvedPolicies.LeavePolicy
			?? throw new BusinessException(HRErrorCodes.LeavePolicyNotFound);

		if (academicYearId.HasValue && leavePolicy.AcademicYearId != academicYearId.Value)
		{
			throw new BusinessException(HRErrorCodes.LeavePolicyNotFound);
		}

		if (!leavePolicy.IsActive || leavePolicy.Status != HrPolicyStatus.Active)
		{
			throw new BusinessException(HRErrorCodes.LeavePolicyInactive);
		}

		var rule = leavePolicy.FindActiveRule(paidType);
		if (rule == null)
		{
			throw new BusinessException(HRErrorCodes.LeavePolicyRuleNotFound);
		}

		var requestedUnits = await _leaveWorkCalendarUnitService.CalculateUnitsAsync(
			employeeId,
			startDate,
			endDate,
			halfDay,
			academicYearId);
		if (requestedUnits <= 0)
		{
			throw new BusinessException(HRErrorCodes.LeaveNoBusinessDaysInRange);
		}

		EnsureRuleAllowsRequest(rule, requestedUnits, halfDay, hasAttachment);
		await EnsureSufficientLeaveBalanceAsync(employeeId, rule, requestedUnits);

		var daysDifference = (startDate.Date - today).Days;
		if (enforceEmployeeTimingRules && daysDifference < rule.MinNoticeDays)
		{
			throw new BusinessException(HRErrorCodes.LeaveNoticePeriod);
		}

		return new LeaveRequestPolicyResolution(leavePolicy, rule, requestedUnits);
	}

	public async Task EnsureSufficientLeaveBalanceAsync(
		Guid employeeId,
		Guid leavePolicyRuleId,
		DateTime startDate,
		DateTime endDate,
		bool halfDay = false)
	{
		var rule = await _leavePolicyRuleRepository.GetAsync(leavePolicyRuleId);
		EnsureValidHalfDayRange(startDate, endDate, halfDay);
		var requestedUnits = LeaveDurationCalculator.CountLeaveUnits(startDate, endDate, halfDay);
		await EnsureSufficientLeaveBalanceAsync(employeeId, rule, requestedUnits);
	}

	public decimal CalculateBusinessDays(DateTime startDate, DateTime endDate)
	{
		return LeaveDurationCalculator.CountInclusiveBusinessDays(startDate, endDate);
	}

	public async Task<decimal> GetRemainingLeaveBalanceAsync(
		Guid employeeId,
		Guid leavePolicyRuleId)
	{
		var rule = await _leavePolicyRuleRepository.FindAsync(leavePolicyRuleId);
		if (rule == null)
		{
			return 0;
		}

		var approvedDays = await _employeeLeaveRepository.GetApprovedLeaveDaysByPolicyAndPaidTypeAsync(
			employeeId,
			rule.LeavePolicyId,
			rule.PaidType);

		if (rule.IsUnlimited || rule.AllowNegativeBalance)
		{
			return decimal.MaxValue;
		}

		return Math.Max(0, (rule.LeaveCount ?? 0) - approvedDays);
	}

	private static void EnsureRuleAllowsRequest(
		LeavePolicyRule rule,
		decimal requestedUnits,
		bool halfDay,
		bool hasAttachment)
	{
		if (halfDay && !rule.AllowHalfDay)
		{
			throw new BusinessException(HRErrorCodes.LeaveHalfDayNotAllowed);
		}

		if (requestedUnits > rule.MaxDaysPerRequest)
		{
			throw new BusinessException(HRErrorCodes.LeaveMaxDaysExceeded);
		}

		if (rule.AttachmentRequiredAfterDays.HasValue &&
		    requestedUnits >= rule.AttachmentRequiredAfterDays.Value &&
		    !hasAttachment)
		{
			throw new BusinessException(HRErrorCodes.LeaveAttachmentRequired);
		}
	}

	private async Task EnsureSufficientLeaveBalanceAsync(Guid employeeId, LeavePolicyRule rule, decimal requestedUnits)
	{
		if (rule.IsUnlimited || rule.AllowNegativeBalance)
		{
			return;
		}

		var approvedDaysForRule = await _employeeLeaveRepository.GetApprovedLeaveDaysByPolicyAndPaidTypeAsync(
			employeeId,
			rule.LeavePolicyId,
			rule.PaidType);
		var remainingBalance = (rule.LeaveCount ?? 0) - approvedDaysForRule;
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

public sealed record LeaveRequestPolicyResolution(
	LeavePolicy LeavePolicy,
	LeavePolicyRule Rule,
	decimal RequestedUnits);
