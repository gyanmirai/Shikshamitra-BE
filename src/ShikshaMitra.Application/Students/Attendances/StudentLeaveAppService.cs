using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.Attendances;

/// <summary>
/// Application service for managing student leave requests
/// </summary>
[Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_LeaveAndAbsent.View)]
public class StudentLeaveAppService : ApplicationService, IStudentLeaveAppService
{
	private readonly IStudentLeaveRepository _studentLeaveRepository;
	private readonly StudentLeaveManager _studentLeaveManager;
	
	public StudentLeaveAppService(
		IStudentLeaveRepository studentLeaveRepository,
		StudentLeaveManager studentLeaveManager)
	{
		_studentLeaveRepository = studentLeaveRepository;
		_studentLeaveManager = studentLeaveManager;
	}
	
    [Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_LeaveAndAbsent.View)]
	public async Task<PagedResultDto<StudentLeaveDto>> GetListAsync(GetStudentLeaveListInput input)
	{
		var totalCount = await _studentLeaveRepository.GetCountAsync(
			input.FilterText,
			input.StudentId,
			input.EducationMediumId,
			input.StandardId,
			input.DivisionId,
			input.StudentLeaveTypeId,
			input.Status,
			input.StartDate,
			input.EndDate,
			CurrentTenant.Id);
		
		var leaves = await _studentLeaveRepository.GetListWithDetailsAsync(
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
			CurrentTenant.Id);
		
		var leaveDtos = leaves.Select(leave => MapToDto(leave)).ToList();
		
		return new PagedResultDto<StudentLeaveDto>(totalCount, leaveDtos);
	}
	
    [Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_LeaveAndAbsent.View)]
	public async Task<StudentLeaveDto> GetAsync(Guid id)
	{
		var leave = await _studentLeaveRepository.GetWithDetailsAsync(id);
		
		if (leave == null)
		{
			throw new BusinessException("StudentLeave:NotFound")
				.WithData("Id", id);
		}
		
		return MapToDto(leave);
	}
	
    [Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_LeaveAndAbsent.Add)]
	public async Task<StudentLeaveDto> CreateAsync(CreateStudentLeaveDto input)
	{
		var leave = await _studentLeaveManager.CreateAsync(
			input.StudentId,
			input.StudentLeaveTypeId,
			input.IsHalfDay,
			input.LeaveDate,
			input.EndDate,
			input.Description,
			input.Attachment,
			input.AttachmentFileName,
			input.AttachmentContentType);
		
		await _studentLeaveRepository.InsertAsync(leave, autoSave: true);
		
		// Reload with details for complete DTO
		var leaveWithDetails = await _studentLeaveRepository.GetWithDetailsAsync(leave.Id);
		
		return MapToDto(leaveWithDetails!);
	}
	
    [Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_LeaveAndAbsent.Add)]
	public async Task<StudentLeaveDto> UpdateAsync(Guid id, UpdateStudentLeaveDto input)
	{
		var leave = await _studentLeaveRepository.GetAsync(id);
		
		if (leave == null)
		{
			throw new BusinessException("StudentLeave:NotFound")
				.WithData("Id", id);
		}
		
		await _studentLeaveManager.UpdateAsync(
			leave,
			input.StudentLeaveTypeId,
			input.IsHalfDay,
			input.LeaveDate,
			input.EndDate,
			input.Description,
			input.Attachment,
			input.AttachmentFileName,
			input.AttachmentContentType);
		
		await _studentLeaveRepository.UpdateAsync(leave, autoSave: true);
		
		// Reload with details for complete DTO
		var leaveWithDetails = await _studentLeaveRepository.GetWithDetailsAsync(leave.Id);
		
		return MapToDto(leaveWithDetails!);
	}
	
    [Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_LeaveAndAbsent.Delete)]
	public async Task DeleteAsync(Guid id)
	{
		var leave = await _studentLeaveRepository.GetAsync(id);
		
		if (leave == null)
		{
			throw new BusinessException("StudentLeave:NotFound")
				.WithData("Id", id);
		}
		
		// Optional: Only allow deletion of pending leaves
		if (leave.Status != StudentLeaveStatusEnum.Pending)
		{
			throw new BusinessException("StudentLeave:CannotDeleteNonPending")
				.WithData("Status", leave.Status);
		}
		
		await _studentLeaveRepository.DeleteAsync(leave, autoSave: true);
	}
	
    [Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_LeaveAndAbsent.Add)]
	public async Task<StudentLeaveDto> ApproveAsync(Guid id, ApproveRejectStudentLeaveDto input)
	{
		if (input.Status != StudentLeaveStatusEnum.Approved)
		{
			throw new BusinessException("StudentLeave:InvalidApproveStatus")
				.WithData("ExpectedStatus", StudentLeaveStatusEnum.Approved)
				.WithData("ActualStatus", input.Status);
		}
		
		var leave = await _studentLeaveRepository.GetAsync(id);
		
		if (leave == null)
		{
			throw new BusinessException("StudentLeave:NotFound")
				.WithData("Id", id);
		}
		
		await _studentLeaveManager.ApproveAsync(leave, input.Remarks);
		
		await _studentLeaveRepository.UpdateAsync(leave, autoSave: true);
		
		// Reload with details for complete DTO
		var leaveWithDetails = await _studentLeaveRepository.GetWithDetailsAsync(leave.Id);
		
		return MapToDto(leaveWithDetails!);
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
		
		var leave = await _studentLeaveRepository.GetAsync(id);
		
		if (leave == null)
		{
			throw new BusinessException("StudentLeave:NotFound")
				.WithData("Id", id);
		}
		
		await _studentLeaveManager.RejectAsync(leave, input.RejectionReason, input.Remarks);
		
		await _studentLeaveRepository.UpdateAsync(leave, autoSave: true);
		
		// Reload with details for complete DTO
		var leaveWithDetails = await _studentLeaveRepository.GetWithDetailsAsync(leave.Id);
		
		return MapToDto(leaveWithDetails!);
	}
	
    [Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_LeaveAndAbsent.View)]
	public async Task<StudentLeaveAttachmentDto> GetAttachmentAsync(Guid id)
	{
		var leave = await _studentLeaveRepository.GetAsync(id);
		
		if (leave == null)
		{
			throw new BusinessException("StudentLeave:NotFound")
				.WithData("Id", id);
		}
		
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
	private StudentLeaveDto MapToDto(StudentLeave leave)
	{
		return new StudentLeaveDto
		{
			Id = leave.Id,
			StudentId = leave.StudentId,
			StudentName = leave.Student != null 
				? $"{leave.Student.FirstName} {leave.Student.LastName}".Trim()
				: string.Empty,
			OrganizationName = null, // TODO: Map from EducationMedium if navigation exists
			ClassName = null, // TODO: Map from Standard if navigation exists
			DivisionName = null, // TODO: Map from Division if navigation exists
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
}
