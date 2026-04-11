using System;
using System.Threading.Tasks;
using ShikshaMitra.HR;
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
		Guid leaveTypeId,
		DateTimeOffset startDate,
		DateTimeOffset endDate,
		string description,
		bool halfDay = false)
	{
		var leaveId = guidGenerator.Create();
		var tenantId = CurrentTenant?.Id ?? Guid.Empty;

		var leave = EmployeeLeave.Create(
			leaveId,
			tenantId,
			employeeId,
			leaveTypeId,
			startDate,
			endDate,
			description,
			halfDay);

		return Task.FromResult(leave);
	}

	public async Task<EmployeeLeave> UpdateLeaveRequestStatusAsync(EmployeeLeave employeeLeave,
		EmployeeLeaveStatusType status, string? rejectionReason)
	{
		employeeLeave.UpdateLeaveStatus(status, rejectionReason);
		return await employeeLeaveRepository.UpdateAsync(employeeLeave);
	}
}