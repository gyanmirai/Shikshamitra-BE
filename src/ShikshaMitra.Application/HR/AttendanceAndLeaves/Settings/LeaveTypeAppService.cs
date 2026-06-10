using System;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

[RemoteService(false)]
public class LeaveTypeAppService(
	IRepository<LeaveType, Guid> repository,
	LeaveTypeManager leaveTypeManager,
	LeaveTypeCacheService cacheService,
	LeavePolicyCacheService leavePolicyCacheService,
	LeaveTypeReadService readService) : CrudAppService<
		LeaveType,
		LeaveTypeDto,
		Guid,
		PagedAndSortedResultRequestDto,
		CreateLeaveTypeDto,
		UpdateLeaveTypeDto>(repository), ILeaveTypeAppService
{
	public override Task<LeaveTypeDto> CreateAsync(CreateLeaveTypeDto input)
	{
		throw new BusinessException(HRErrorCodes.LeaveTypeCreateMethodNotSupported);
	}

	public override async Task<LeaveTypeDto> UpdateAsync(Guid id, UpdateLeaveTypeDto input)
	{
		var leaveType = await Repository.GetAsync(id);
		await leaveTypeManager.UpdateAsync(leaveType, input.Type, input.Color, input.Description);
		await Repository.UpdateAsync(leaveType, autoSave: true);
		await ClearCachesAsync(leaveType.AcademicYearId);
		return ObjectMapper.Map<LeaveType, LeaveTypeDto>(leaveType);
	}

	public override async Task DeleteAsync(Guid id)
	{
		var leaveType = await Repository.GetAsync(id);
		leaveType.Deactivate();
		await Repository.UpdateAsync(leaveType, autoSave: true);
		await ClearCachesAsync(leaveType.AcademicYearId);
	}

	public override Task<PagedResultDto<LeaveTypeDto>> GetListAsync(PagedAndSortedResultRequestDto input) =>
		readService.GetListAsync(input);

	public Task<PagedResultDto<LeaveTypeDto>> GetAllActiveAsync(PagedResultRequestDto input) =>
		readService.GetAllActiveAsync(input);

	public Task<LeaveTypeListResponse> GetListByAcademicYearIdAsync(
		Guid academicYearId,
		int page,
		int pageSize,
		string? search = null,
		CancellationToken cancellationToken = default) =>
		readService.GetListByAcademicYearIdAsync(academicYearId, page, pageSize, search, cancellationToken);

	public async Task<LeaveTypeResponse> CreateLeaveTypeAsync(Guid academicYearId, LeaveTypeCreateRequest input)
	{
		var leaveType = await leaveTypeManager.CreateAsync(
			academicYearId,
			input.Name,
			input.Color,
			input.Description);

		await Repository.InsertAsync(leaveType, autoSave: true);
		await ClearCachesAsync(academicYearId);

		return LeaveTypeReadService.ToResponse(leaveType);
	}

	public async Task<LeaveTypeResponse> GetLeaveTypeByIdAsync(Guid leaveTypeId)
	{
		var leaveType = await Repository.GetAsync(leaveTypeId);
		return LeaveTypeReadService.ToResponse(leaveType);
	}

	public async Task<LeaveTypeResponse> UpdateLeaveTypeAsync(Guid leaveTypeId, LeaveTypeUpdateRequest input)
	{
		var leaveType = await Repository.GetAsync(leaveTypeId);
		await leaveTypeManager.UpdateAsync(
			leaveType,
			input.Name ?? leaveType.Type,
			input.Color ?? leaveType.Color,
			input.Description);
		await Repository.UpdateAsync(leaveType, autoSave: true);
		await ClearCachesAsync(leaveType.AcademicYearId);

		return LeaveTypeReadService.ToResponse(leaveType);
	}

	public async Task<LeaveTypeDeleteResponse> DeleteLeaveTypeAsync(Guid leaveTypeId)
	{
		var leaveType = await Repository.GetAsync(leaveTypeId);
		leaveType.Deactivate();
		await Repository.UpdateAsync(leaveType, autoSave: true);
		await ClearCachesAsync(leaveType.AcademicYearId);

		return LeaveTypeReadService.ToDeleteResponse(leaveTypeId);
	}

	private async Task ClearCachesAsync(Guid academicYearId)
	{
		await cacheService.ClearActiveAsync();
		await cacheService.ClearContractAsync(academicYearId);
		await leavePolicyCacheService.ClearActiveAsync();
		await leavePolicyCacheService.ClearContractAsync(academicYearId);
	}
}
