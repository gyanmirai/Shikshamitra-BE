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
public class LeaveRequestActionWorkflow(
	IEmployeeLeaveRepository employeeLeaveRepository,
	EmployeeLeaveManager employeeLeaveManager,
	LeaveValidationService leaveValidationService,
	LeaveActorAccessService leaveActorAccessService)
	: ApplicationService
{
	public async Task<LeaveActionResultDto> ApproveAsync(Guid leaveRequestId)
	{
		var query = await employeeLeaveRepository.WithDetailsAsync(x => x.Employee!, x => x.LeavePolicy!);
		var leave = await AsyncExecuter.FirstOrDefaultAsync(query.Where(x => x.Id == leaveRequestId))
			?? throw new EntityNotFoundException(typeof(EmployeeLeave), leaveRequestId);

		EnsureNotTerminalForApproverAction(leave.Status);
		if (!IsValidStatusTransition(leave.Status, EmployeeLeaveStatusType.Approved))
		{
			throw new BusinessException(HRErrorCodes.LeaveCannotActOnLeaveState);
		}

		var actorContext = await leaveActorAccessService.ResolveContextAsync();
		if (!actorContext.IsAdminBypass)
		{
			await leaveValidationService.EnsureActorMayChangeLeaveStatusAsync(
				leave.EmployeeId,
				actorContext.ActorEmployeeId!.Value);
		}

		await leaveValidationService.EnsureSufficientLeaveBalanceAsync(
			leave.EmployeeId,
			leave.LeaveTypeId,
			leave.StartDate.DateTime,
			leave.EndDate.DateTime,
			leave.HalfDay);

		await employeeLeaveManager.ApproveAsync(
			leave,
			leave.LeavePolicy!.PaidType,
			LeaveManagementProjection.CalculateDays(leave.StartDate.DateTime, leave.EndDate.DateTime, leave.HalfDay));

		return new LeaveActionResultDto
		{
			Id = leave.Id,
			Status = EmployeeLeaveStatusType.Approved.ToString().ToUpperInvariant(),
			Message = "Leave request approved successfully"
		};
	}

	public async Task<LeaveActionResultDto> RejectAsync(Guid leaveRequestId, LeaveRejectionRequestDto input)
	{
		var query = await employeeLeaveRepository.WithDetailsAsync(x => x.Employee!);
		var leave = await AsyncExecuter.FirstOrDefaultAsync(query.Where(x => x.Id == leaveRequestId))
			?? throw new EntityNotFoundException(typeof(EmployeeLeave), leaveRequestId);

		EnsureNotTerminalForApproverAction(leave.Status);
		if (!IsValidStatusTransition(leave.Status, EmployeeLeaveStatusType.Rejected))
		{
			throw new BusinessException(HRErrorCodes.LeaveCannotActOnLeaveState);
		}

		var actorContext = await leaveActorAccessService.ResolveContextAsync();
		if (!actorContext.IsAdminBypass)
		{
			await leaveValidationService.EnsureActorMayChangeLeaveStatusAsync(
				leave.EmployeeId,
				actorContext.ActorEmployeeId!.Value);
		}

		await employeeLeaveManager.UpdateLeaveRequestStatusAsync(
			leave,
			EmployeeLeaveStatusType.Rejected,
			rejectionReason: input.Remarks);

		return new LeaveActionResultDto
		{
			Id = leave.Id,
			Status = EmployeeLeaveStatusType.Rejected.ToString().ToUpperInvariant(),
			Message = "Leave request rejected successfully"
		};
	}

	public async Task<LeaveRequestDeleteDto> DeleteAsync(Guid leaveRequestId)
	{
		var query = await employeeLeaveRepository.GetQueryableAsync();
		var leave = await AsyncExecuter.FirstOrDefaultAsync(query.Where(x => x.Id == leaveRequestId))
			?? throw new EntityNotFoundException(typeof(EmployeeLeave), leaveRequestId);

		if (leave.Status == EmployeeLeaveStatusType.Cancelled)
		{
			throw new BusinessException(HRErrorCodes.LeaveAlreadyCancelled);
		}

		var actorContext = await leaveActorAccessService.ResolveContextAsync();
		if (!actorContext.IsAdminBypass && leave.EmployeeId != actorContext.ActorEmployeeId!.Value)
		{
			throw new BusinessException(HRErrorCodes.LeaveDeleteOwnOnly);
		}

		await employeeLeaveManager.CancelAsync(leave);

		return new LeaveRequestDeleteDto { Id = leave.Id };
	}

	public async Task<LeaveRequestDeleteDto> DeleteMyAsync(Guid leaveRequestId)
	{
		var actorEmployeeId = await leaveActorAccessService.ResolveCurrentActorEmployeeIdStrictAsync();

		var query = await employeeLeaveRepository.GetQueryableAsync();
		var leave = await AsyncExecuter.FirstOrDefaultAsync(query.Where(x => x.Id == leaveRequestId))
			?? throw new EntityNotFoundException(typeof(EmployeeLeave), leaveRequestId);

		LeaveActorAccessService.EnsureLeaveBelongsToActor(leave.EmployeeId, actorEmployeeId);

		if (leave.Status == EmployeeLeaveStatusType.Cancelled)
		{
			throw new BusinessException(HRErrorCodes.LeaveAlreadyCancelled);
		}

		await employeeLeaveManager.CancelAsync(leave);
		return new LeaveRequestDeleteDto { Id = leave.Id };
	}

	private static void EnsureNotTerminalForApproverAction(EmployeeLeaveStatusType status)
	{
		if (status is EmployeeLeaveStatusType.Approved
		    or EmployeeLeaveStatusType.Rejected
		    or EmployeeLeaveStatusType.Cancelled)
		{
			throw new BusinessException(HRErrorCodes.LeaveConflictTerminalState);
		}
	}

	private static bool IsValidStatusTransition(EmployeeLeaveStatusType from, EmployeeLeaveStatusType to)
	{
		return (from, to) switch
		{
			(EmployeeLeaveStatusType.Pending, EmployeeLeaveStatusType.Reviewed) => true,
			(EmployeeLeaveStatusType.Pending, EmployeeLeaveStatusType.Approved) => true,
			(EmployeeLeaveStatusType.Pending, EmployeeLeaveStatusType.Rejected) => true,
			(EmployeeLeaveStatusType.Pending, EmployeeLeaveStatusType.Cancelled) => true,
			(EmployeeLeaveStatusType.Reviewed, EmployeeLeaveStatusType.Approved) => true,
			(EmployeeLeaveStatusType.Reviewed, EmployeeLeaveStatusType.Rejected) => true,
			(EmployeeLeaveStatusType.Reviewed, EmployeeLeaveStatusType.Cancelled) => true,
			_ => false
		};
	}
}
