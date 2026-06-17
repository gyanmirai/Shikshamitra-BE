using ShikshaMitra.Common;
using ShikshaMitra.HR.AttendanceAndLeaves.LeaveManagementInfo;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.TenantSettings.AcademicYears;
using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

[RemoteService(false)]
public class LeaveRequestCreationWorkflow(
	IEmployeeLeaveRepository employeeLeaveRepository,
	IEmployeeRepository employeeRepository,
	EmployeeLeaveManager employeeLeaveManager,
	LeaveValidationService leaveValidationService,
	IAcademicYearRepository academicYearRepository,
	LeaveActorAccessService leaveActorAccessService)
	: ApplicationService
{
	public async Task<LeaveRequestCreatedDto> CreateAsync(
		Guid academicYearId,
		LeaveRequestCreateDto input,
		object? attachment)
	{
		var academicYear = await academicYearRepository.FindAsync(academicYearId)
			?? throw new EntityNotFoundException(typeof(AcademicYear), academicYearId);

		var start = input.StartDate.Date;
		var end = input.EndDate.Date;
		if (start < academicYear.StartDate.Date || end > academicYear.EndDate.Date)
		{
			throw new BusinessException(HRErrorCodes.LeaveDatesOutsideAcademicYear);
		}

		var actorContext = await leaveActorAccessService.ResolveContextAsync();

		if (string.IsNullOrWhiteSpace(input.PaidType))
		{
			throw new BusinessException(HRErrorCodes.LeavePolicyRuleNotFound);
		}
		var paidType = LeavePolicyProjection.ParsePaidType(input.PaidType);

		var targetEmployeeId = input.ForEmployeeId
			?? actorContext.ActorEmployeeId
			?? throw new BusinessException(HRErrorCodes.LeaveTargetEmployeeRequired);

		await leaveActorAccessService.EnsureMayCreateLeaveOnBehalfAsync(targetEmployeeId, actorContext);
		var isExplicitOnBehalfRequest = input.ForEmployeeId.HasValue &&
			(!actorContext.ActorEmployeeId.HasValue ||
			input.ForEmployeeId.Value != actorContext.ActorEmployeeId.Value);
		var enforceEmployeeTimingRules = !isExplicitOnBehalfRequest;

		var targetEmployee = await employeeRepository.FindAsync(targetEmployeeId)
			?? throw new EntityNotFoundException(typeof(Employee), targetEmployeeId);

		if ((targetEmployee.EmployeeStatusType ?? EmployeeStatusType.Active) != EmployeeStatusType.Active)
		{
			throw new BusinessException(HRErrorCodes.LeaveTargetEmployeeInactive);
		}

		var attachmentBytes = await LeaveAttachmentProcessor.ExtractAttachmentBytesAsync(attachment);

		var resolved = await leaveValidationService.ValidateLeaveRequestAsync(
			targetEmployee.Id,
			paidType,
			input.StartDate,
			input.EndDate,
			input.HalfDay,
			academicYearId,
			enforceEmployeeTimingRules,
			hasAttachment: attachmentBytes is { Length: > 0 });

		var leave = await employeeLeaveManager.CreateAsync(
			targetEmployee.Id,
			resolved.LeavePolicy.Id,
			resolved.Rule.Id,
			resolved.Rule.PaidType,
			new DateTimeOffset(input.StartDate),
			new DateTimeOffset(input.EndDate),
			input.Reason,
			input.HalfDay);

		if (attachmentBytes is { Length: > 0 })
		{
			leave.Attachment = attachmentBytes;
		}

		await employeeLeaveRepository.InsertAsync(leave, autoSave: true);

		return new LeaveRequestCreatedDto
		{
			Id = leave.Id,
			PublicId = leave.PublicId,
			Status = "PENDING"
		};
	}
}
