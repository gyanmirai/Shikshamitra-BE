using System;
using System.Threading;
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

	/// <summary>
	/// Get paginated leave policies for an organization (matches OpenAPI contract)
	/// </summary>
	Task<LeavePolicyListResponse> GetListByMediumStageIdAsync(
		Guid mediumStageId,
		int page,
		int pageSize,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Create a leave policy (matches OpenAPI contract)
	/// </summary>
	Task<LeavePolicyResponse> CreateLeavePolicyAsync(LeavePolicyCreateRequest input);

	/// <summary>
	/// Update a leave policy (matches OpenAPI contract)
	/// </summary>
	Task<LeavePolicyUpdatedResponse> UpdateLeavePolicyAsync(Guid policyId, LeavePolicyCreateRequest input);

	/// <summary>
	/// Delete (deactivate) a leave policy (matches OpenAPI contract)
	/// </summary>
	Task<LeavePolicyDeleteResponse> DeleteLeavePolicyAsync(Guid policyId);
}
