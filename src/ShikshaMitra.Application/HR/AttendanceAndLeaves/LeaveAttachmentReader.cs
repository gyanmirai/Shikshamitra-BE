using ShikshaMitra.Common;
using ShikshaMitra.HR.AttendanceAndLeaves.LeaveManagementInfo;
using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

[RemoteService(false)]
public class LeaveAttachmentReader(
	IEmployeeLeaveRepository employeeLeaveRepository,
	LeaveActorAccessService leaveActorAccessService)
	: ApplicationService
{
	public async Task<LeaveAttachmentDto> GetAttachmentAsync(Guid leaveRequestId)
	{
		var leave = await GetLeaveAsync(leaveRequestId);
		await leaveActorAccessService.EnsureActorCanAccessEmployeeAsync(leave.EmployeeId);

		return BuildAttachmentDto(leave, leaveRequestId);
	}

	public async Task<LeaveAttachmentDto> GetMyAttachmentAsync(Guid leaveRequestId)
	{
		var actorEmployeeId = await leaveActorAccessService.ResolveCurrentActorEmployeeIdStrictAsync();
		var leave = await GetLeaveAsync(leaveRequestId);

		LeaveActorAccessService.EnsureLeaveBelongsToActor(leave.EmployeeId, actorEmployeeId);

		return BuildAttachmentDto(leave, leaveRequestId);
	}

	private async Task<EmployeeLeave> GetLeaveAsync(Guid leaveRequestId)
	{
		var query = await employeeLeaveRepository.GetQueryableAsync();
		return await AsyncExecuter.FirstOrDefaultAsync(query.Where(x => x.Id == leaveRequestId))
			?? throw new EntityNotFoundException(typeof(EmployeeLeave), leaveRequestId);
	}

	private static LeaveAttachmentDto BuildAttachmentDto(EmployeeLeave leave, Guid leaveRequestId)
	{
		if (leave.Attachment is not { Length: > 0 })
		{
			throw new BusinessException(HRErrorCodes.LeaveAttachmentNotFound);
		}

		var (contentType, extension) = LeaveAttachmentProcessor.DetectContentType(leave.Attachment);
		return new LeaveAttachmentDto
		{
			FileContent = leave.Attachment,
			ContentType = contentType,
			FileName = $"leave-attachment-{leaveRequestId:N}{extension}"
		};
	}
}
