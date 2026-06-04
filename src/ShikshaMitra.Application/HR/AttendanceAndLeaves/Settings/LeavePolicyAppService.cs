using System;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

[RemoteService(false)]
public class LeavePolicyAppService(
	IRepository<LeavePolicy, Guid> repository,
	LeavePolicyReadService readService,
	LeavePolicyMutationWorkflow mutationWorkflow)
	: CrudAppService<
		LeavePolicy,
		LeavePolicyDto,
		Guid,
		PagedAndSortedResultRequestDto,
		CreateLeavePolicyDto,
		UpdateLeavePolicyDto>(repository), ILeavePolicyAppService
{
	public override Task<LeavePolicyDto> CreateAsync(CreateLeavePolicyDto input)
	{
		throw new BusinessException(HRErrorCodes.LeavePolicyCreateMethodNotSupported);
	}

	public override Task<LeavePolicyDto> UpdateAsync(Guid id, UpdateLeavePolicyDto input)
	{
		throw new BusinessException(HRErrorCodes.LeavePolicyUpdateMethodNotSupported);
	}

	public override Task DeleteAsync(Guid id) =>
		mutationWorkflow.DeactivateAsync(id);

	public override Task<PagedResultDto<LeavePolicyDto>> GetListAsync(PagedAndSortedResultRequestDto input) =>
		readService.GetListAsync(input);

	public Task<PagedResultDto<LeavePolicyDto>> GetAllActiveAsync(PagedResultRequestDto input) =>
		readService.GetAllActiveAsync(input);

	public Task<LeavePolicyListResponse> GetListByAcademicYearIdAsync(
		Guid academicYearId,
		int page,
		int pageSize,
		string? search = null,
		CancellationToken cancellationToken = default) =>
		readService.GetListByAcademicYearIdAsync(academicYearId, page, pageSize, search, cancellationToken);

	public Task<LeavePolicyResponse> CreateLeavePolicyAsync(Guid academicYearId, LeavePolicyCreateRequest input) =>
		mutationWorkflow.CreateLeavePolicyAsync(academicYearId, input);

	public Task<LeavePolicyDetailResponse> GetLeavePolicyByIdAsync(Guid policyId) =>
		readService.GetLeavePolicyByIdAsync(policyId);

	public Task<LeavePolicyDetailResponse> UpdateLeavePolicyAsync(Guid policyId, LeavePolicyUpdateRequest input) =>
		mutationWorkflow.UpdateLeavePolicyAsync(policyId, input);

	public Task<LeavePolicyDeleteResponse> DeleteLeavePolicyAsync(Guid policyId) =>
		mutationWorkflow.DeleteLeavePolicyAsync(policyId);
}
