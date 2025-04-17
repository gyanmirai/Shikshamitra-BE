using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class EmployeeLeaveAppService(
	EmployeeLeaveManager employeeLeaveManager,
	IEmployeeLeaveRepository employeeLeaveRepository) :
	CrudAppService<EmployeeLeave, EmployeeLeaveDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateEmployeeLeaveDto>(
		employeeLeaveRepository),
	IEmployeeLeaveService
{
	public override async Task<EmployeeLeaveDto> UpdateAsync(Guid id, CreateUpdateEmployeeLeaveDto input)
	{
		EmployeeLeave employeeLeave = await employeeLeaveRepository.GetAsync(id);

		// check if the leave request is pending then only update the leave request
		employeeLeaveManager.IsLeaveStatusIsPending(employeeLeave);

		EmployeeLeaveDto employeeLeaveDto = await base.UpdateAsync(id, input);
		return employeeLeaveDto;
	}

	public async Task<EmployeeLeaveDto> UpdateLeaveRequestStatusAsync(Guid id, LeaveApprovalDto input)
	{
		EmployeeLeave employeeLeave = await employeeLeaveRepository.GetAsync(id);
		employeeLeave =
			await employeeLeaveManager.UpdateLeaveRequestStatusAsync(employeeLeave, input.Status,
				input.RejectionReason);
		return ObjectMapper.Map<EmployeeLeave, EmployeeLeaveDto>(employeeLeave);
	}
}