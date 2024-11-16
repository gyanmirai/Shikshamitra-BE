using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public interface IEmployeeLeaveService : ICrudAppService<EmployeeLeaveDto, Guid, PagedAndSortedResultRequestDto,
	CreateUpdateEmployeeLeaveDto>
{
}