using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

[RemoteService(false)]
[Authorize]
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

	[Authorize(ShikshaMitraSubmodulePermissions.HR.LeaveManagement_SettingLeavePolicy.Archive)]
	public override Task DeleteAsync(Guid id) =>
		mutationWorkflow.DeactivateAsync(id);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.LeaveManagement_SettingLeavePolicy.View)]
	public override Task<LeavePolicyDto> GetAsync(Guid id) =>
		base.GetAsync(id);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.LeaveManagement_SettingLeavePolicy.View)]
	public override Task<PagedResultDto<LeavePolicyDto>> GetListAsync(PagedAndSortedResultRequestDto input) =>
		readService.GetListAsync(input);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.LeaveManagement_SettingLeavePolicy.View)]
	public Task<PagedResultDto<LeavePolicyDto>> GetAllActiveAsync(PagedResultRequestDto input) =>
		readService.GetAllActiveAsync(input);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.LeaveManagement_SettingLeavePolicy.View)]
	public Task<LeavePolicyListResponse> GetListByAcademicYearIdAsync(
		Guid academicYearId,
		int page,
		int pageSize,
		string? search = null,
		bool? archivedOnly = null,
		CancellationToken cancellationToken = default) =>
		readService.GetListByAcademicYearIdAsync(academicYearId, page, pageSize, search, archivedOnly, cancellationToken);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.LeaveManagement_SettingLeavePolicy.Create)]
	public Task<LeavePolicyResponse> CreateLeavePolicyAsync(Guid academicYearId, LeavePolicyCreateRequest input) =>
		mutationWorkflow.CreateLeavePolicyAsync(academicYearId, input);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.LeaveManagement_SettingLeavePolicy.View)]
	public Task<LeavePolicyDetailResponse> GetLeavePolicyByIdAsync(Guid policyId) =>
		readService.GetLeavePolicyByIdAsync(policyId);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.LeaveManagement_SettingLeavePolicy.Edit)]
	public Task<LeavePolicyDetailResponse> UpdateLeavePolicyAsync(Guid policyId, LeavePolicyUpdateRequest input) =>
		mutationWorkflow.UpdateLeavePolicyAsync(policyId, input);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.LeaveManagement_SettingLeavePolicy.SetDefault)]
	public Task<LeavePolicyDetailResponse> SetDefaultAsync(Guid policyId) =>
		mutationWorkflow.SetDefaultAsync(policyId);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.LeaveManagement_SettingLeavePolicy.Archive)]
	public Task<LeavePolicyDetailResponse> UnarchiveAsync(Guid policyId) =>
		mutationWorkflow.UnarchiveAsync(policyId);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.LeaveManagement_SettingLeavePolicy.Archive)]
	public Task<LeavePolicyDeleteResponse> DeleteLeavePolicyAsync(Guid policyId) =>
		mutationWorkflow.DeleteLeavePolicyAsync(policyId);
}
