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
	
	/// <summary>
	/// Get paginated leave types for an organization (matches OpenAPI contract)
	/// </summary>
	Task<LeaveTypeListResponse> GetListByMediumStageIdAsync(Guid mediumStageId, int page, int pageSize);
	
	/// <summary>
	/// Create a leave type (matches OpenAPI contract)
	/// </summary>
	Task<LeaveTypeResponse> CreateLeaveTypeAsync(LeaveTypeCreateRequest input);
	
	/// <summary>
	/// Get leave type by ID (matches OpenAPI contract)
	/// </summary>
	Task<LeaveTypeResponse> GetLeaveTypeByIdAsync(Guid leaveTypeId);
	
	/// <summary>
	/// Update a leave type (matches OpenAPI contract)
	/// </summary>
	Task<LeaveTypeResponse> UpdateLeaveTypeAsync(Guid leaveTypeId, LeaveTypeUpdateRequest input);
	
	/// <summary>
	/// Delete (deactivate) a leave type (matches OpenAPI contract)
	/// </summary>
	Task<LeaveTypeDeleteResponse> DeleteLeaveTypeAsync(Guid leaveTypeId);
}
