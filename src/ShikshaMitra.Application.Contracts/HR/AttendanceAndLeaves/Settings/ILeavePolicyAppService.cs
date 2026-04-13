using System;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

public interface ILeavePolicyAppService : ICrudAppService<
	LeavePolicyDto,
	Guid,
	PagedAndSortedResultRequestDto,
	CreateLeavePolicyDto,
	UpdateLeavePolicyDto>
{
	Task<PagedResultDto<LeavePolicyDto>> GetAllActiveAsync(PagedResultRequestDto input);

	Task<LeavePolicyListResponse> GetListByAcademicYearIdAsync(
		Guid academicYearId,
		int page,
		int pageSize,
		string? search = null,
		CancellationToken cancellationToken = default);

	Task<LeavePolicyResponse> CreateLeavePolicyAsync(Guid academicYearId, LeavePolicyCreateRequest input);
	Task<LeavePolicyDetailResponse> GetLeavePolicyByIdAsync(Guid policyId);
	Task<LeavePolicyDetailResponse> UpdateLeavePolicyAsync(Guid policyId, LeavePolicyUpdateRequest input);
	Task<LeavePolicyDeleteResponse> DeleteLeavePolicyAsync(Guid policyId);
}
