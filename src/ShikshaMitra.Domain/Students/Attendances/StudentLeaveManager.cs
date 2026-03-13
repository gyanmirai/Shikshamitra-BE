using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Students.Attendance.Settings;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;

namespace ShikshaMitra.Students.Attendances;

/// <summary>
/// Domain manager for StudentLeave entity - handles business logic and validation
/// </summary>
public class StudentLeaveManager(
	IRepository<Student, Guid> studentRepository,
	IStudentLeaveTypeRepository studentLeaveTypeRepository,
	IGuidGenerator guidGenerator
) : DomainService
{
	/// <summary>
	/// Creates a new student leave request with validation
	/// </summary>
	public async Task<StudentLeave> CreateAsync(
		Guid studentId,
		Guid studentLeaveTypeId,
		bool isHalfDay,
		DateTimeOffset leaveDate,
		DateTimeOffset? endDate,
		string? description,
		byte[]? attachment,
		string? attachmentFileName,
		string? attachmentContentType)
	{
		// Validate student exists
		var student = await studentRepository.GetAsync(studentId);
		if (student == null)
		{
			throw new BusinessException("StudentLeave:StudentNotFound")
				.WithData("StudentId", studentId);
		}

		// Validate leave type exists and is active
		var leaveType = await studentLeaveTypeRepository.GetAsync(studentLeaveTypeId);
		if (leaveType == null)
		{
			throw new BusinessException("StudentLeave:LeaveTypeNotFound")
				.WithData("LeaveTypeId", studentLeaveTypeId);
		}

		if (!leaveType.IsActive)
		{
			throw new BusinessException("StudentLeave:LeaveTypeNotActive")
				.WithData("LeaveTypeId", studentLeaveTypeId);
		}

		// Validate dates
		ValidateDates(isHalfDay, leaveDate, endDate);

		// Validate attachment
		ValidateAttachment(attachment, attachmentFileName, attachmentContentType);

		// Create the leave entity
		var leave = new StudentLeave(
			guidGenerator.Create(),
			CurrentTenant.Id,
			studentId,
			student.AcademicMediumId,
			student.StandardId,
			student.DivisionId,
			studentLeaveTypeId,
			isHalfDay,
			leaveDate,
			endDate,
			description,
			attachment,
			attachmentFileName,
			attachmentContentType);

		return leave;
	}

	/// <summary>
	/// Updates an existing student leave request (only if status is Pending)
	/// </summary>
	public async Task<StudentLeave> UpdateAsync(
		StudentLeave studentLeave,
		Guid studentLeaveTypeId,
		bool isHalfDay,
		DateTimeOffset leaveDate,
		DateTimeOffset? endDate,
		string? description,
		byte[]? attachment,
		string? attachmentFileName,
		string? attachmentContentType)
	{
		// Check if leave is in Pending status
		if (studentLeave.Status != StudentLeaveStatusEnum.Pending)
		{
			throw new BusinessException("StudentLeave:CannotUpdateNonPending")
				.WithData("Status", studentLeave.Status);
		}

		// Validate leave type exists and is active
		var leaveType = await studentLeaveTypeRepository.GetAsync(studentLeaveTypeId);
		if (leaveType == null)
		{
			throw new BusinessException("StudentLeave:LeaveTypeNotFound")
				.WithData("LeaveTypeId", studentLeaveTypeId);
		}

		if (!leaveType.IsActive)
		{
			throw new BusinessException("StudentLeave:LeaveTypeNotActive")
				.WithData("LeaveTypeId", studentLeaveTypeId);
		}

		// Validate dates
		ValidateDates(isHalfDay, leaveDate, endDate);

		// Validate attachment
		ValidateAttachment(attachment, attachmentFileName, attachmentContentType);

		// Update the leave entity
		studentLeave.UpdateLeaveRequest(
			studentLeaveTypeId,
			isHalfDay,
			leaveDate,
			endDate,
			description,
			attachment,
			attachmentFileName,
			attachmentContentType);

		return studentLeave;
	}

	/// <summary>
	/// Approves a student leave request
	/// </summary>
	public async Task<StudentLeave> ApproveAsync(StudentLeave studentLeave, string? remarks = null)
	{
		if (studentLeave.Status != StudentLeaveStatusEnum.Pending)
		{
			throw new BusinessException("StudentLeave:CannotApproveNonPending")
				.WithData("Status", studentLeave.Status);
		}

		studentLeave.Approve(remarks);
		return await Task.FromResult(studentLeave);
	}

	/// <summary>
	/// Rejects a student leave request with a reason
	/// </summary>
	public async Task<StudentLeave> RejectAsync(
		StudentLeave studentLeave,
		string rejectionReason,
		string? remarks = null)
	{
		if (studentLeave.Status != StudentLeaveStatusEnum.Pending)
		{
			throw new BusinessException("StudentLeave:CannotRejectNonPending")
				.WithData("Status", studentLeave.Status);
		}

		Check.NotNullOrWhiteSpace(rejectionReason, nameof(rejectionReason));

		studentLeave.Reject(rejectionReason, remarks);
		return await Task.FromResult(studentLeave);
	}

	/// <summary>
	/// Validates leave dates based on business rules
	/// </summary>
	private void ValidateDates(bool isHalfDay, DateTimeOffset leaveDate, DateTimeOffset? endDate)
	{
		// Validate leave date is not too far in the past
		var maxPastDate = Clock.Now.AddDays(-StudentLeaveConsts.MaxPastDays);
		if (leaveDate < maxPastDate)
		{
			throw new BusinessException("StudentLeave:LeaveDateTooFarInPast")
				.WithData("MaxPastDays", StudentLeaveConsts.MaxPastDays);
		}

		if (isHalfDay)
		{
			// For half-day leave, end date must be null
			if (endDate != null)
			{
				throw new BusinessException("StudentLeave:HalfDayCannotHaveEndDate");
			}
		}
		else
		{
			// For full-day leave, end date is required
			if (endDate == null)
			{
				throw new BusinessException("StudentLeave:FullDayRequiresEndDate");
			}

			// End date must be >= start date
			if (endDate < leaveDate)
			{
				throw new BusinessException("StudentLeave:EndDateBeforeStartDate");
			}

			// Validate leave duration doesn't exceed maximum
			var duration = (endDate.Value.Date - leaveDate.Date).Days + 1;
			if (duration > StudentLeaveConsts.MaxLeaveDurationDays)
			{
				throw new BusinessException("StudentLeave:LeaveDurationTooLong")
					.WithData("MaxDays", StudentLeaveConsts.MaxLeaveDurationDays)
					.WithData("RequestedDays", duration);
			}
		}
	}

	/// <summary>
	/// Validates attachment file
	/// </summary>
	private void ValidateAttachment(byte[]? attachment, string? attachmentFileName, string? attachmentContentType)
	{
		if (attachment == null)
		{
			return; // Attachment is optional
		}

		// Validate file size
		if (attachment.Length > StudentLeaveConsts.MaxAttachmentSize)
		{
			throw new BusinessException("StudentLeave:AttachmentTooLarge")
				.WithData("MaxSize", StudentLeaveConsts.MaxAttachmentSize)
				.WithData("ActualSize", attachment.Length);
		}

		// Validate filename and content type are provided
		if (string.IsNullOrWhiteSpace(attachmentFileName))
		{
			throw new BusinessException("StudentLeave:AttachmentFileNameRequired");
		}

		if (string.IsNullOrWhiteSpace(attachmentContentType))
		{
			throw new BusinessException("StudentLeave:AttachmentContentTypeRequired");
		}

		// Validate content type is allowed
		if (!StudentLeaveConsts.AllowedContentTypes.Contains(attachmentContentType.ToLowerInvariant()))
		{
			throw new BusinessException("StudentLeave:AttachmentContentTypeNotAllowed")
				.WithData("ContentType", attachmentContentType)
				.WithData("AllowedTypes", string.Join(", ", StudentLeaveConsts.AllowedContentTypes));
		}
	}

	/// <summary>
	/// Calculates total days for a leave period
	/// </summary>
	public decimal CalculateTotalDays(DateTimeOffset startDate, DateTimeOffset? endDate, bool isHalfDay)
	{
		if (isHalfDay)
		{
			return 0.5m;
		}

		if (endDate == null)
		{
			throw new ArgumentNullException(nameof(endDate), "End date is required for full-day leave");
		}

		// Calculate total days (inclusive of both start and end date)
		var days = (endDate.Value.Date - startDate.Date).Days + 1;
		return days;
	}
}
