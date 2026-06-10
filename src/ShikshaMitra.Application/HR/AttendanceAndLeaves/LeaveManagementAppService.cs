using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.AttendanceAndLeaves.LeaveManagementInfo;
using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

[Authorize]
[RemoteService(false)]
public class LeaveManagementAppService(
	LeaveActorAccessService leaveActorAccessService,
	LeaveManagementReader leaveManagementReader,
	LeaveBalanceReader leaveBalanceReader,
	LeaveRequestCreationWorkflow creationWorkflow,
	LeaveRequestActionWorkflow actionWorkflow,
	LeaveAttachmentReader attachmentReader,
	LeaveManagementExportService exportService)
	: ApplicationService, ILeaveManagementAppService
{
	public Task<PagedLeaveManagementResultDto> GetListAsync(LeaveManagementGetListInput input) =>
		leaveManagementReader.GetListAsync(input);

	public Task<PagedLeaveManagementResultDto> GetMyListAsync(LeaveManagementGetListInput input) =>
		leaveManagementReader.GetMyListAsync(input);

	public Task<LeaveRequestDetailsDto> GetAsync(Guid leaveRequestId) =>
		leaveManagementReader.GetAsync(leaveRequestId);

	public async Task<LeaveRequestDetailsDto> GetMyAsync(Guid leaveRequestId)
	{
		var actorEmployeeId = await leaveActorAccessService.ResolveCurrentActorEmployeeIdStrictAsync();
		var details = await leaveManagementReader.GetAsync(leaveRequestId);
		LeaveActorAccessService.EnsureLeaveBelongsToActor(details.EmployeeId, actorEmployeeId);
		return details;
	}

	public Task<LeaveRequestCreatedDto> CreateAsync(Guid academicYearId, LeaveRequestCreateDto input) =>
		CreateAsync(academicYearId, input, attachment: null);

	public Task<LeaveRequestCreatedDto> CreateMyAsync(Guid academicYearId, LeaveRequestCreateDto input) =>
		CreateMyAsync(academicYearId, input, attachment: null);

	public async Task<LeaveRequestCreatedDto> CreateMyAsync(
		Guid academicYearId,
		LeaveRequestCreateDto input,
		object? attachment)
	{
		var actorEmployeeId = await leaveActorAccessService.ResolveCurrentActorEmployeeIdStrictAsync();
		var selfRequest = new LeaveRequestCreateDto
		{
			ForEmployeeId = actorEmployeeId,
			LeaveTypeId = input.LeaveTypeId,
			StartDate = input.StartDate,
			EndDate = input.EndDate,
			HalfDay = input.HalfDay,
			Reason = input.Reason
		};
		return await CreateAsync(academicYearId, selfRequest, attachment);
	}

	public Task<LeaveRequestCreatedDto> CreateAsync(
		Guid academicYearId,
		LeaveRequestCreateDto input,
		object? attachment) =>
		creationWorkflow.CreateAsync(academicYearId, input, attachment);

	public Task<LeaveActionResultDto> ApproveAsync(Guid leaveRequestId, LeaveApprovalRequestDto? input) =>
		actionWorkflow.ApproveAsync(leaveRequestId);

	public Task<LeaveActionResultDto> RejectAsync(Guid leaveRequestId, LeaveRejectionRequestDto input) =>
		actionWorkflow.RejectAsync(leaveRequestId, input);

	public Task<LeaveRequestDeleteDto> DeleteAsync(Guid leaveRequestId) =>
		actionWorkflow.DeleteAsync(leaveRequestId);

	public Task<LeaveRequestDeleteDto> DeleteMyAsync(Guid leaveRequestId) =>
		actionWorkflow.DeleteMyAsync(leaveRequestId);

	public Task<LeaveAttachmentDto> GetAttachmentAsync(Guid leaveRequestId) =>
		attachmentReader.GetAttachmentAsync(leaveRequestId);

	public Task<LeaveAttachmentDto> GetMyAttachmentAsync(Guid leaveRequestId) =>
		attachmentReader.GetMyAttachmentAsync(leaveRequestId);

	public Task<LeaveBalanceListDto> GetBalancesAsync(Guid academicYearId, Guid? forEmployeeId) =>
		leaveBalanceReader.GetBalancesAsync(academicYearId, forEmployeeId);

	public async Task<LeaveBalanceListDto> GetMyBalancesAsync(Guid academicYearId)
	{
		var actorEmployeeId = await leaveActorAccessService.ResolveCurrentActorEmployeeIdStrictAsync();
		return await leaveBalanceReader.GetBalancesAsync(academicYearId, actorEmployeeId);
	}

	public Task<LeaveManagementCalendarResultDto> GetCalendarAsync(LeaveManagementCalendarInput input) =>
		leaveManagementReader.GetCalendarAsync(input);

	public Task<byte[]> ExportToExcelAsync(LeaveManagementGetListInput input) =>
		exportService.ExportToExcelAsync(input);
}
