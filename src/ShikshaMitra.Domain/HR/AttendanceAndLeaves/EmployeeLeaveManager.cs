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

	public async Task<EmployeeLeave> UpdateLeaveRequestStatusAsync(EmployeeLeave employeeLeave,
		EmployeeLeaveStatusType status, string? rejectionReason)
	{
		employeeLeave.UpdateLeaveStatus(status, rejectionReason);
		return await employeeLeaveRepository.UpdateAsync(employeeLeave);
	}
}