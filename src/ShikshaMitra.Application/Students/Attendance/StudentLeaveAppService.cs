using System;
using System.Data;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.Students.StudentEnrollments;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.Attendance;

/// <summary>
/// Application service for managing student leave requests
/// </summary>
[RemoteService(false)]
public class StudentLeaveAppService(
	IStudentLeaveRepository studentLeaveRepository,
	StudentLeaveManager studentLeaveManager,
	IStudentRepository studentRepository,
	StudentScopeAccessService studentScopeAccessService,
	IRepository<AcademicYear, Guid> years,
	IRepository<StudentEnrollment, Guid> enrollments,
	AttendancePolicyResolver attendancePolicies
) : ApplicationService, IStudentLeaveAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_LeaveAndAbsent.View)]
	public async Task<PagedResultDto<StudentLeaveDto>> GetListAsync(GetStudentLeaveListInput input)
	{
		var scopedStudents = await studentScopeAccessService.ApplyCurrentUserScopeAsync(
			await studentRepository.GetQueryableWithoutAutoIncludesAsync());
		var scopedStudentIds = await AsyncExecuter.ToListAsync(scopedStudents.Select(s => s.Id));
		if (input.AcademicYearId.HasValue)
		{
			await studentScopeAccessService.EnsureCanAccessAcademicYearAsync(input.AcademicYearId.Value, "read attendance leave requests");
			var year = await years.GetAsync(y => y.Id == input.AcademicYearId.Value && y.TenantId == CurrentTenant.Id);
			var membership = await enrollments.GetListAsync(e => e.TenantId == CurrentTenant.Id && e.AcademicYearId == year.Id);
			scopedStudentIds = scopedStudentIds.Intersect(membership.Select(e => e.StudentId)).ToList();
			var policy = await attendancePolicies.ResolveAsync(year.Id, Clock.Now.Date);
			var zone = TimeZoneInfo.FindSystemTimeZoneById(policy.Options.SchoolTimeZone);
			var yearStart = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(year.StartDate.Date, DateTimeKind.Unspecified), zone));
			var yearEnd = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(year.EndDate.Date.AddDays(1), DateTimeKind.Unspecified), zone)).AddTicks(-1);
			input.StartDate = input.StartDate.HasValue && input.StartDate > yearStart ? input.StartDate : yearStart;
			input.EndDate = input.EndDate.HasValue && input.EndDate < yearEnd ? input.EndDate : yearEnd;
		}

		var totalCount = await studentLeaveRepository.GetCountAsync(
			input.FilterText,
			input.StudentId,
			input.EducationMediumId,
			input.StandardId,
			input.DivisionId,
			input.StudentLeaveTypeId,
			input.Status,
			input.StartDate,
			input.EndDate,
			scopedStudentIds,
			CurrentTenant.Id);

		var leaves = await studentLeaveRepository.GetListWithDetailsAsync(
			input.FilterText,
			input.StudentId,
			input.EducationMediumId,
			input.StandardId,
			input.DivisionId,
			input.StudentLeaveTypeId,
			input.Status,
			input.StartDate,
			input.EndDate,
			input.Sorting,
			input.MaxResultCount,
			input.SkipCount,
			scopedStudentIds,
			CurrentTenant.Id);

		var leaveDtos = leaves.Select(leave => MapToDto(leave)).ToList();

		return new PagedResultDto<StudentLeaveDto>(totalCount, leaveDtos);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_LeaveAndAbsent.View)]
	public async Task<StudentLeaveDto> GetAsync(Guid id)
	{
		var leave = await studentLeaveRepository.GetWithDetailsAsync(id);

		if (leave == null)
		{
			throw new BusinessException("StudentLeave:NotFound")
				.WithData("Id", id);
		}
		await studentScopeAccessService.EnsureCanAccessLeaveAsync(leave, nameof(GetAsync));

		return MapToDto(leave);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_LeaveAndAbsent.Add)]
	public async Task<StudentLeaveDto> CreateAsync(CreateStudentLeaveDto input)
	{
		var student = await studentScopeAccessService.GetStudentAsync(input.StudentId, nameof(CreateAsync));
		var leave = await studentLeaveManager.CreateAsync(
			student,
			input.StudentLeaveTypeId,
			input.IsHalfDay,
			input.LeaveDate,
			input.EndDate,
			input.Description,
			input.Attachment,
			input.AttachmentFileName,
			input.AttachmentContentType);

		await studentLeaveRepository.InsertAsync(leave, autoSave: true);

		// Reload with details for complete DTO
		var leaveWithDetails = await studentLeaveRepository.GetWithDetailsAsync(leave.Id);

		return MapToDto(leaveWithDetails!);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_LeaveAndAbsent.Add)]
	public async Task<StudentLeaveDto> UpdateAsync(Guid id, UpdateStudentLeaveDto input)
	{
		var leave = await studentLeaveRepository.GetAsync(id);

		if (leave == null)
		{
			throw new BusinessException("StudentLeave:NotFound")
				.WithData("Id", id);
		}
		await studentScopeAccessService.EnsureCanAccessLeaveAsync(leave, nameof(UpdateAsync));

		await studentLeaveManager.UpdateAsync(
			leave,
			input.StudentLeaveTypeId,
			input.IsHalfDay,
			input.LeaveDate,
			input.EndDate,
			input.Description,
			input.Attachment,
			input.AttachmentFileName,
			input.AttachmentContentType);

		await studentLeaveRepository.UpdateAsync(leave, autoSave: true);

		// Reload with details for complete DTO
		var leaveWithDetails = await studentLeaveRepository.GetWithDetailsAsync(leave.Id);

		return MapToDto(leaveWithDetails!);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_LeaveAndAbsent.Delete)]
	public async Task DeleteAsync(Guid id)
	{
		var leave = await studentLeaveRepository.GetAsync(id);

		if (leave == null)
		{
			throw new BusinessException("StudentLeave:NotFound")
				.WithData("Id", id);
		}
		await studentScopeAccessService.EnsureCanAccessLeaveAsync(leave, nameof(DeleteAsync));

		// Optional: Only allow deletion of pending leaves
		if (leave.Status != StudentLeaveStatusEnum.Pending)
		{
			throw new BusinessException("StudentLeave:CannotDeleteNonPending")
				.WithData("Status", leave.Status);
		}

		await studentLeaveRepository.DeleteAsync(leave, autoSave: true);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_LeaveAndAbsent.Add)]
	public Task<StudentLeaveDto> ApproveAsync(Guid id, ApproveRejectStudentLeaveDto input) => AttendanceCommandConflictMapper.ExecuteAsync(() => ApproveCoreAsync(id, input));
	private async Task<StudentLeaveDto> ApproveCoreAsync(Guid id, ApproveRejectStudentLeaveDto input)
	{
		using var work = UnitOfWorkManager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true, IsolationLevel = IsolationLevel.Serializable }, requiresNew: true);
		if (input.Status != StudentLeaveStatusEnum.Approved)
		{
			throw new BusinessException("StudentLeave:InvalidApproveStatus")
				.WithData("ExpectedStatus", StudentLeaveStatusEnum.Approved)
				.WithData("ActualStatus", input.Status);
		}

		var leave = await studentLeaveRepository.GetAsync(id);

		if (leave == null)
		{
			throw new BusinessException("StudentLeave:NotFound")
				.WithData("Id", id);
		}
		await studentScopeAccessService.EnsureCanAccessLeaveAsync(leave, nameof(ApproveAsync));
		var student = await studentScopeAccessService.GetStudentAsync(leave.StudentId, nameof(ApproveAsync));
		var policy = await attendancePolicies.ResolveAsync(student.AcademicYearId, leave.LeaveDate.UtcDateTime.Date);
		var zone = TimeZoneInfo.FindSystemTimeZoneById(policy.Options.SchoolTimeZone);
		var schoolStart = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(TimeZoneInfo.ConvertTime(leave.LeaveDate, zone).Date, zone));
		var schoolEnd = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(TimeZoneInfo.ConvertTime(leave.EndDate ?? leave.LeaveDate, zone).Date.AddDays(1), zone));
		var overlapping = await studentLeaveRepository.GetApprovedForAttendanceAsync([leave.StudentId], schoolStart, schoolEnd, CurrentTenant.Id);
		if (overlapping.Any(l => l.Id != leave.Id))
			throw new BusinessException(AttendanceOperationErrorCodes.OverlappingApprovedLeave);

		await studentLeaveManager.ApproveAsync(leave, input.Remarks);

		await studentLeaveRepository.UpdateAsync(leave, autoSave: true);

		// Reload with details for complete DTO
		var leaveWithDetails = await studentLeaveRepository.GetWithDetailsAsync(leave.Id);

		var result = MapToDto(leaveWithDetails!); await work.CompleteAsync(); return result;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_LeaveAndAbsent.Add)]
	public async Task<StudentLeaveDto> RejectAsync(Guid id, ApproveRejectStudentLeaveDto input)
	{
		if (input.Status != StudentLeaveStatusEnum.Rejected)
		{
			throw new BusinessException("StudentLeave:InvalidRejectStatus")
				.WithData("ExpectedStatus", StudentLeaveStatusEnum.Rejected)
				.WithData("ActualStatus", input.Status);
		}

		if (string.IsNullOrWhiteSpace(input.RejectionReason))
		{
			throw new BusinessException("StudentLeave:RejectionReasonRequired");
		}

		var leave = await studentLeaveRepository.GetAsync(id);

		if (leave == null)
		{
			throw new BusinessException("StudentLeave:NotFound")
				.WithData("Id", id);
		}
		await studentScopeAccessService.EnsureCanAccessLeaveAsync(leave, nameof(RejectAsync));

		await studentLeaveManager.RejectAsync(leave, input.RejectionReason, input.Remarks);

		await studentLeaveRepository.UpdateAsync(leave, autoSave: true);

		// Reload with details for complete DTO
		var leaveWithDetails = await studentLeaveRepository.GetWithDetailsAsync(leave.Id);

		return MapToDto(leaveWithDetails!);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_LeaveAndAbsent.View)]
	public async Task<StudentLeaveAttachmentDto> GetAttachmentAsync(Guid id)
	{
		var leave = await studentLeaveRepository.GetAsync(id);

		if (leave == null)
		{
			throw new BusinessException("StudentLeave:NotFound")
				.WithData("Id", id);
		}
		await studentScopeAccessService.EnsureCanAccessLeaveAsync(leave, nameof(GetAttachmentAsync));

		if (leave.Attachment == null || leave.Attachment.Length == 0)
		{
			throw new BusinessException("StudentLeave:NoAttachment")
				.WithData("Id", id);
		}

		return new StudentLeaveAttachmentDto
		{
			FileContent = leave.Attachment,
			FileName = leave.AttachmentFileName ?? "attachment",
			ContentType = leave.AttachmentContentType ?? "application/octet-stream"
		};
	}

	/// <summary>
	/// Maps StudentLeave entity to DTO with computed fields
	/// </summary>
	private StudentLeaveDto MapToDto(StudentLeave leave) => new StudentLeaveDto
	{
		Id = leave.Id,
		StudentId = leave.StudentId,
		StudentName = leave.Student != null
				? $"{leave.Student.FirstName} {leave.Student.LastName}".Trim()
				: string.Empty,
		OrganizationName = leave.Student?.AcademicMedium?.Name,
		ClassName = leave.Student?.Standard?.Name,
		DivisionName = leave.Student?.Division?.Name,
		StudentLeaveTypeId = leave.StudentLeaveTypeId,
		LeaveTypeName = leave.StudentLeaveType?.LeaveTypeName ?? string.Empty,
		LeaveTypeColor = leave.StudentLeaveType?.Color ?? "#000000",
		IsHalfDay = leave.IsHalfDay,
		LeaveDate = leave.LeaveDate,
		EndDate = leave.EndDate,
		TotalDays = leave.TotalDays,
		Description = leave.Description,
		Remarks = leave.Remarks,
		Status = leave.Status,
		RejectionReason = leave.RejectionReason,
		HasAttachment = leave.Attachment != null && leave.Attachment.Length > 0,
		AttachmentFileName = leave.AttachmentFileName,
		CreationTime = leave.CreationTime,
		CreatorId = leave.CreatorId,
		LastModificationTime = leave.LastModificationTime,
		LastModifierId = leave.LastModifierId
	};

}
