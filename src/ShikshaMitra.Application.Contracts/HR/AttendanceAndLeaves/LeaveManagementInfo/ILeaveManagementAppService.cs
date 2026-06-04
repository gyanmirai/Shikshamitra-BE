using System;
using System.Threading.Tasks;

namespace ShikshaMitra.HR.AttendanceAndLeaves.LeaveManagementInfo;

public interface ILeaveManagementAppService
{
	Task<PagedLeaveManagementResultDto> GetListAsync(LeaveManagementGetListInput input);
	Task<PagedLeaveManagementResultDto> GetMyListAsync(LeaveManagementGetListInput input);

	Task<LeaveRequestDetailsDto> GetAsync(Guid leaveRequestId);
	Task<LeaveRequestDetailsDto> GetMyAsync(Guid leaveRequestId);

	Task<LeaveRequestCreatedDto> CreateAsync(Guid academicYearId, LeaveRequestCreateDto input);
	Task<LeaveRequestCreatedDto> CreateMyAsync(Guid academicYearId, LeaveRequestCreateDto input);
	Task<LeaveRequestCreatedDto> CreateMyAsync(Guid academicYearId, LeaveRequestCreateDto input, object? attachment);

	Task<LeaveRequestCreatedDto> CreateAsync(Guid academicYearId, LeaveRequestCreateDto input, object? attachment);

	Task<LeaveActionResultDto> ApproveAsync(Guid leaveRequestId, LeaveApprovalRequestDto? input);

	Task<LeaveActionResultDto> RejectAsync(Guid leaveRequestId, LeaveRejectionRequestDto input);

	Task<LeaveRequestDeleteDto> DeleteAsync(Guid leaveRequestId);
	Task<LeaveRequestDeleteDto> DeleteMyAsync(Guid leaveRequestId);

	Task<LeaveAttachmentDto> GetAttachmentAsync(Guid leaveRequestId);
	Task<LeaveAttachmentDto> GetMyAttachmentAsync(Guid leaveRequestId);

	Task<LeaveBalanceListDto> GetBalancesAsync(Guid academicYearId, Guid? forEmployeeId);
	Task<LeaveBalanceListDto> GetMyBalancesAsync(Guid academicYearId);

	Task<LeaveManagementCalendarResultDto> GetCalendarAsync(LeaveManagementCalendarInput input);

	Task<byte[]> ExportToExcelAsync(LeaveManagementGetListInput input);
}
