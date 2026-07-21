using System;
using System.Threading.Tasks;
using ShikshaMitra.HR;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using Volo.Abp;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class EmployeeLeaveManager(IEmployeeLeaveRepository employeeLeaveRepository, IGuidGenerator guidGenerator)
	: DomainService
{
	public EmployeeLeave IsLeaveStatusIsPending(EmployeeLeave employeeLeave)
	{
		if (employeeLeave.Status != EmployeeLeaveStatusType.Pending)
		{
			throw new BusinessException(HRErrorCodes.LeaveInvalidStatusTransition);
		}

		return employeeLeave;
	}

	public Task<EmployeeLeave> CreateAsync(
		Guid employeeId,
		Guid leavePolicyId,
		Guid leavePolicyRuleId,
		LeavePaidType paidType,
		DateTimeOffset startDate,
		DateTimeOffset endDate,
		string description,
		bool halfDay = false,
		bool autoApprove = false,
		decimal approvedLeaveUnits = 0m)
	{
		var leaveId = guidGenerator.Create();
		var tenantId = CurrentTenant.Id;

		var leave = EmployeeLeave.Create(
			leaveId,
			tenantId,
			employeeId,
			leavePolicyId,
			leavePolicyRuleId,
			paidType,
			startDate,
			endDate,
			description,
			halfDay);

		if (autoApprove)
		{
			leave.SetApprovalPayrollSnapshot(paidType, approvedLeaveUnits);
			leave.UpdateLeaveStatus(EmployeeLeaveStatusType.Approved, null);
		}

		return Task.FromResult(leave);
	}

	public async Task<EmployeeLeave> UpdateLeaveRequestStatusAsync(EmployeeLeave employeeLeave,
		EmployeeLeaveStatusType status, string? rejectionReason)
	{
		employeeLeave.UpdateLeaveStatus(status, rejectionReason);
		return await employeeLeaveRepository.UpdateAsync(employeeLeave);
	}

	public async Task<EmployeeLeave> ApproveAsync(
		EmployeeLeave employeeLeave,
		LeavePaidType paidType,
		decimal approvedLeaveUnits)
	{
		employeeLeave.SetApprovalPayrollSnapshot(paidType, approvedLeaveUnits);
		employeeLeave.UpdateLeaveStatus(EmployeeLeaveStatusType.Approved, null);
		return await employeeLeaveRepository.UpdateAsync(employeeLeave);
	}

	/// <summary>
	/// Soft-cancel a leave request: transitions to Cancelled status with audit retention.
	/// </summary>
	public async Task<EmployeeLeave> CancelAsync(EmployeeLeave employeeLeave, bool allowApproved = false)
	{
		employeeLeave.Cancel(allowApproved);
		return await employeeLeaveRepository.UpdateAsync(employeeLeave);
	}
}
