using System;
using System.Threading.Tasks;
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
			throw new BusinessException("Leave request is already approved or rejected");
		}

		return employeeLeave;
	}

	public async Task<EmployeeLeave> CreateAsync(
		Guid employeeId,
		Guid leaveTypeId,
		DateTimeOffset startDate,
		DateTimeOffset endDate,
		string description,
		bool halfDay = false)
	{
		var leaveId = guidGenerator.Create();
		var tenantId = CurrentTenant?.Id ?? Guid.Empty;

		// Create new EmployeeLeave using reflection since constructor is internal
		var leave = Activator.CreateInstance(
			typeof(EmployeeLeave),
			System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
			null,
			new object[] { leaveId, tenantId, halfDay, startDate, endDate, description, (string?)null },
			null) as EmployeeLeave;

		if (leave == null)
		{
			throw new InvalidOperationException("Failed to create EmployeeLeave instance");
		}

		// Set the EmployeeId and LeaveTypeId using reflection
		typeof(EmployeeLeave).GetProperty("EmployeeId")?.SetValue(leave, employeeId);
		typeof(EmployeeLeave).GetProperty("LeaveTypeId")?.SetValue(leave, leaveTypeId);

		return leave;
	}

	public async Task<EmployeeLeave> UpdateLeaveRequestStatusAsync(EmployeeLeave employeeLeave,
		EmployeeLeaveStatusType status, string? rejectionReason)
	{
		employeeLeave.UpdateLeaveStatus(status, rejectionReason);
		return await employeeLeaveRepository.UpdateAsync(employeeLeave);
	}
}