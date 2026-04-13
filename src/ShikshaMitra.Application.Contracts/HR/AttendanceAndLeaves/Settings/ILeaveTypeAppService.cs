using System;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

public interface ILeaveTypeAppService : ICrudAppService<
	LeaveTypeDto,
	Guid,
	PagedAndSortedResultRequestDto,
	CreateLeaveTypeDto,
	UpdateLeaveTypeDto>
{
	Task<PagedResultDto<LeaveTypeDto>> GetAllActiveAsync(PagedResultRequestDto input);

	Task<LeaveTypeListResponse> GetListByAcademicYearIdAsync(
		Guid academicYearId,
		int page,
		int pageSize,
		string? search = null,
		CancellationToken cancellationToken = default);

	Task<LeaveTypeResponse> CreateLeaveTypeAsync(Guid academicYearId, LeaveTypeCreateRequest input);
	Task<LeaveTypeResponse> GetLeaveTypeByIdAsync(Guid leaveTypeId);
	Task<LeaveTypeResponse> UpdateLeaveTypeAsync(Guid leaveTypeId, LeaveTypeUpdateRequest input);
	Task<LeaveTypeDeleteResponse> DeleteLeaveTypeAsync(Guid leaveTypeId);
}
