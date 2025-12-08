using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

/// <summary>
/// Application service interface for managing employee leave types
/// </summary>
public interface ILeaveTypeAppService : ICrudAppService<
	LeaveTypeDto,
	Guid,
	PagedAndSortedResultRequestDto,
	CreateLeaveTypeDto,
	UpdateLeaveTypeDto>
{
	/// <summary>
	/// Get all active leave types with pagination support
	/// </summary>
	Task<PagedResultDto<LeaveTypeDto>> GetAllActiveAsync(PagedResultRequestDto input);
}
