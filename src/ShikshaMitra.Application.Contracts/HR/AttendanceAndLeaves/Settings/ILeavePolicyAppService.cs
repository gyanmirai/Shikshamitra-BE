using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

/// <summary>
/// Application service interface for managing employee leave policies
/// </summary>
public interface ILeavePolicyAppService : ICrudAppService<
	LeavePolicyDto,
	Guid,
	PagedAndSortedResultRequestDto,
	CreateLeavePolicyDto,
	UpdateLeavePolicyDto>
{
	/// <summary>
	/// Get all active leave policies with pagination support
	/// </summary>
	Task<PagedResultDto<LeavePolicyDto>> GetAllActiveAsync(PagedResultRequestDto input);
}
