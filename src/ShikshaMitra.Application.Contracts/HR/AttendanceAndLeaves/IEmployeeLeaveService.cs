using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public interface IEmployeeLeaveService : ICrudAppService<EmployeeLeaveDto, Guid, PagedAndSortedResultRequestDto,
	CreateUpdateEmployeeLeaveDto>
{
	Task<EmployeeLeaveDto> UpdateLeaveRequestStatusAsync(Guid id, LeaveApprovalDto input);
}