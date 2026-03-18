using System;
using ShikshaMitra.Students.Attendance.Settings;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.Attendances;

/// <summary>
/// Represents a student leave request with approval workflow
/// </summary>
public class StudentLeave : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }

	/// <summary>
	/// Reference to the student requesting leave
	/// </summary>
	public Guid StudentId { get; private set; }

	/// <summary>
	/// Denormalized education medium ID for filtering (from student record)
	/// </summary>
	public Guid? EducationMediumId { get; private set; }

	/// <summary>
	/// Denormalized standard/class ID for filtering (from student record)
	/// </summary>
	public Guid? StandardId { get; private set; }

	/// <summary>
	/// Denormalized division ID for filtering (from student record)
	/// </summary>
	public Guid? DivisionId { get; private set; }

	/// <summary>
	/// Reference to the type of leave (sick, casual, etc.)
	/// </summary>
	public Guid StudentLeaveTypeId { get; private set; }

	/// <summary>
	/// Indicates if this is a half-day leave
	/// </summary>
	public bool IsHalfDay { get; private set; }

	/// <summary>
	/// Start date of the leave (or the single date for half-day leave)
	/// </summary>
	public DateTimeOffset LeaveDate { get; private set; }

	/// <summary>
	/// End date of the leave (null for half-day leave)
	/// </summary>
	public DateTimeOffset? EndDate { get; private set; }

	/// <summary>
	/// Total number of leave days (0.5 for half-day, calculated for date range)
	/// </summary>
	public decimal TotalDays { get; private set; }

	/// <summary>
	/// Rich text description/reason for leave
	/// </summary>
	public string? Description { get; private set; }

	/// <summary>
	/// Remarks from admin/approver
	/// </summary>
	public string? Remarks { get; private set; }

	/// <summary>
	/// Current status of the leave request
	/// </summary>
	public StudentLeaveStatusEnum Status { get; private set; }

	/// <summary>
	/// Reason for rejection (required when status is Rejected)
	/// </summary>
	public string? RejectionReason { get; private set; }

	/// <summary>
	/// Attachment file data (max 5MB)
	/// </summary>
	public byte[]? Attachment { get; private set; }

	/// <summary>
	/// Original file name of the attachment
	/// </summary>
	public string? AttachmentFileName { get; private set; }

	/// <summary>
	/// MIME type of the attachment
	/// </summary>
	public string? AttachmentContentType { get; private set; }

	// Navigation properties
	public Student? Student { get; private set; }
	public StudentLeaveType? StudentLeaveType { get; private set; }

	private StudentLeave()
	{
		/* This constructor is for deserialization / ORM purpose */
	}

	internal StudentLeave(
		Guid id,
		Guid? tenantId,
		Guid studentId,
		Guid? educationMediumId,
		Guid? standardId,
		Guid? divisionId,
		Guid studentLeaveTypeId,
		bool isHalfDay,
		DateTimeOffset leaveDate,
		DateTimeOffset? endDate,
		string? description,
		byte[]? attachment,
		string? attachmentFileName,
		string? attachmentContentType)
		: base(id)
	{
		TenantId = tenantId;
		StudentId = studentId;
		EducationMediumId = educationMediumId;
		StandardId = standardId;
		DivisionId = divisionId;
		StudentLeaveTypeId = studentLeaveTypeId;
		IsHalfDay = isHalfDay;
		LeaveDate = leaveDate;
		EndDate = endDate;
		Status = StudentLeaveStatusEnum.Pending;

		SetDescription(description);
		SetAttachment(attachment, attachmentFileName, attachmentContentType);
		CalculateAndSetTotalDays();
	}

	/// <summary>
	/// Updates the leave request details (only allowed when status is Pending)
	/// </summary>
	public void UpdateLeaveRequest(
		Guid studentLeaveTypeId,
		bool isHalfDay,
		DateTimeOffset leaveDate,
		DateTimeOffset? endDate,
		string? description,
		byte[]? attachment,
		string? attachmentFileName,
		string? attachmentContentType)
	{
		if (Status != StudentLeaveStatusEnum.Pending)
		{
			throw new BusinessException("StudentLeave:CannotUpdateNonPending")
				.WithData("Status", Status);
		}

		StudentLeaveTypeId = studentLeaveTypeId;
		IsHalfDay = isHalfDay;
		LeaveDate = leaveDate;
		EndDate = endDate;

		SetDescription(description);
		SetAttachment(attachment, attachmentFileName, attachmentContentType);
		CalculateAndSetTotalDays();
	}

	/// <summary>
	/// Approves the leave request
	/// </summary>
	public void Approve(string? remarks = null)
	{
		if (Status != StudentLeaveStatusEnum.Pending)
		{
			throw new BusinessException("StudentLeave:CannotApproveNonPending")
				.WithData("Status", Status);
		}

		Status = StudentLeaveStatusEnum.Approved;
		SetRemarks(remarks);
		RejectionReason = null; // Clear rejection reason if any
	}

	/// <summary>
	/// Rejects the leave request with a reason
	/// </summary>
	public void Reject(string rejectionReason, string? remarks = null)
	{
		if (Status != StudentLeaveStatusEnum.Pending)
		{
			throw new BusinessException("StudentLeave:CannotRejectNonPending")
				.WithData("Status", Status);
		}

		Check.NotNullOrWhiteSpace(rejectionReason, nameof(rejectionReason));

		Status = StudentLeaveStatusEnum.Rejected;
		SetRejectionReason(rejectionReason);
		SetRemarks(remarks);
	}

	/// <summary>
	/// Sets the description with validation
	/// </summary>
	private void SetDescription(string? description)
	{
		if (!string.IsNullOrEmpty(description) && description.Length > StudentLeaveConsts.MaxDescriptionLength)
		{
			throw new BusinessException("StudentLeave:DescriptionTooLong")
				.WithData("MaxLength", StudentLeaveConsts.MaxDescriptionLength)
				.WithData("ActualLength", description.Length);
		}

		Description = description;
	}

	/// <summary>
	/// Sets remarks with validation
	/// </summary>
	private void SetRemarks(string? remarks)
	{
		if (!string.IsNullOrEmpty(remarks) && remarks.Length > StudentLeaveConsts.MaxRemarksLength)
		{
			throw new BusinessException("StudentLeave:RemarksTooLong")
				.WithData("MaxLength", StudentLeaveConsts.MaxRemarksLength)
				.WithData("ActualLength", remarks.Length);
		}

		Remarks = remarks;
	}

	/// <summary>
	/// Sets rejection reason with validation
	/// </summary>
	private void SetRejectionReason(string rejectionReason)
	{
		Check.NotNullOrWhiteSpace(rejectionReason, nameof(rejectionReason));

		if (rejectionReason.Length > StudentLeaveConsts.MaxRejectionReasonLength)
		{
			throw new BusinessException("StudentLeave:RejectionReasonTooLong")
				.WithData("MaxLength", StudentLeaveConsts.MaxRejectionReasonLength)
				.WithData("ActualLength", rejectionReason.Length);
		}

		RejectionReason = rejectionReason;
	}

	/// <summary>
	/// Sets attachment with validation
	/// </summary>
	private void SetAttachment(byte[]? attachment, string? attachmentFileName, string? attachmentContentType)
	{
		if (attachment != null)
		{
			if (attachment.Length > StudentLeaveConsts.MaxAttachmentSize)
			{
				throw new BusinessException("StudentLeave:AttachmentTooLarge")
					.WithData("MaxSize", StudentLeaveConsts.MaxAttachmentSize)
					.WithData("ActualSize", attachment.Length);
			}

			Check.NotNullOrWhiteSpace(attachmentFileName, nameof(attachmentFileName));
			Check.NotNullOrWhiteSpace(attachmentContentType, nameof(attachmentContentType));

			if (attachmentFileName.Length > StudentLeaveConsts.MaxAttachmentFileNameLength)
			{
				throw new BusinessException("StudentLeave:AttachmentFileNameTooLong")
					.WithData("MaxLength", StudentLeaveConsts.MaxAttachmentFileNameLength);
			}
		}

		Attachment = attachment;
		AttachmentFileName = attachmentFileName;
		AttachmentContentType = attachmentContentType;
	}

	/// <summary>
	/// Calculates and sets the total days based on leave date range
	/// </summary>
	private void CalculateAndSetTotalDays()
	{
		if (IsHalfDay)
		{
			TotalDays = 0.5m;
		}
		else
		{
			if (EndDate == null)
			{
				throw new BusinessException("StudentLeave:EndDateRequiredForFullDay");
			}

			if (EndDate < LeaveDate)
			{
				throw new BusinessException("StudentLeave:EndDateBeforeStartDate");
			}

			// Calculate total days (inclusive of both start and end date)
			var days = (EndDate.Value.Date - LeaveDate.Date).Days + 1;
			TotalDays = days;
		}
	}
}
