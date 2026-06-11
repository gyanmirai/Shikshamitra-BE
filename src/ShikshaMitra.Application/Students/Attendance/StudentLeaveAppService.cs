using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.Attendance;

/// <summary>
/// Application service for managing student leave requests
/// </summary>
[RemoteService(false)]
public class StudentLeaveAppService(
	IStudentLeaveRepository studentLeaveRepository,
	StudentLeaveManager studentLeaveManager,
	IStudentRepository studentRepository,
	StudentScopeAccessService studentScopeAccessService
) : ApplicationService, IStudentLeaveAppService
{
	public async Task<PagedResultDto<StudentLeaveDto>> GetListAsync(GetStudentLeaveListInput input)
	{
		var scopedStudents = await studentScopeAccessService.ApplyCurrentUserScopeAsync(
			await studentRepository.GetQueryableWithoutAutoIncludesAsync());
		var scopedStudentIds = scopedStudents.Select(s => s.Id);

		var query = (await studentLeaveRepository.GetQueryableAsync())
			.Where(l => scopedStudentIds.Contains(l.StudentId));

		if (CurrentTenant.Id.HasValue)
		{
			query = query.Where(l => l.TenantId == CurrentTenant.Id);
		}

		if (!string.IsNullOrWhiteSpace(input.FilterText))
		{
			query = query.Where(l =>
				(l.Description != null && l.Description.Contains(input.FilterText)) ||
				(l.Remarks != null && l.Remarks.Contains(input.FilterText)) ||
				(l.RejectionReason != null && l.RejectionReason.Contains(input.FilterText)));
		}

		if (input.StudentId.HasValue)
		{
			query = query.Where(l => l.StudentId == input.StudentId.Value);
		}

		if (input.EducationMediumId.HasValue)
		{
			query = query.Where(l => l.EducationMediumId == input.EducationMediumId.Value);
		}

		if (input.StandardId.HasValue)
		{
			query = query.Where(l => l.StandardId == input.StandardId.Value);
		}

		if (input.DivisionId.HasValue)
		{
			query = query.Where(l => l.DivisionId == input.DivisionId.Value);
		}

		if (input.StudentLeaveTypeId.HasValue)
		{
			query = query.Where(l => l.StudentLeaveTypeId == input.StudentLeaveTypeId.Value);
		}

		if (input.Status.HasValue)
		{
			query = query.Where(l => l.Status == input.Status.Value);
		}

		if (input.StartDate.HasValue)
		{
			query = query.Where(l => l.LeaveDate >= input.StartDate.Value);
		}

		if (input.EndDate.HasValue)
		{
			query = query.Where(l => l.EndDate == null || l.EndDate <= input.EndDate.Value);
		}

		var totalCount = await AsyncExecuter.CountAsync(query);
		var leaveKeys = await AsyncExecuter.ToListAsync(
			query.Select(l => new
			{
				l.Id,
				l.LeaveDate
			}));

		var leaveIds = leaveKeys
			.OrderByDescending(l => l.LeaveDate)
			.Skip(input.SkipCount)
			.Take(input.MaxResultCount)
			.Select(l => l.Id)
			.ToList();

		var leaves = new List<StudentLeave>();
		foreach (var leaveId in leaveIds)
		{
			var leave = await studentLeaveRepository.GetWithDetailsAsync(leaveId);
			if (leave != null)
			{
				leaves.Add(leave);
			}
		}

		var leaveDtos = leaves.Select(leave => MapToDto(leave)).ToList();

		return new PagedResultDto<StudentLeaveDto>(totalCount, leaveDtos);
	}

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

	public async Task<StudentLeaveDto> ApproveAsync(Guid id, ApproveRejectStudentLeaveDto input)
	{
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

		await studentLeaveManager.ApproveAsync(leave, input.Remarks);

		await studentLeaveRepository.UpdateAsync(leave, autoSave: true);

		// Reload with details for complete DTO
		var leaveWithDetails = await studentLeaveRepository.GetWithDetailsAsync(leave.Id);

		return MapToDto(leaveWithDetails!);
	}

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
