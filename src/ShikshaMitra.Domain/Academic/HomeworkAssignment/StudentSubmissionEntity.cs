using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public class StudentSubmissionEntity : FullAuditedEntity<Guid>, IMultiTenant, IHasConcurrencyStamp
{
	public string ConcurrencyStamp { get; set; } = Guid.NewGuid().ToString("N");
	public string? SubmittedContentHtml { get; private set; }
	public bool Acknowledged { get; private set; }
	public DateTimeOffset? MobileSavedAt { get; private set; }
	public DateTimeOffset? MobileUpdatedAt { get; private set; }
	public Guid? TenantId { get; set; }

	// For Homework submissions (null when this is an assignment submission)
	public Guid? HomeworkAssignmentId { get; set; }
	public HomeworkAssignmentEntity HomeworkAssignment { get; set; }

	// For Assignment submissions (nullable for backward compatibility)
	public Guid? AssignmentId { get; set; }
	public AssignmentEntity Assignment { get; set; }

	public Guid StudentId { get; set; }
	public Guid DivisionId { get; set; }

	public DateTimeOffset? SubmissionDate { get; set; }

	private DateTime _dueDate;
	/// <summary>
	/// Due date for submission. Always stored as UTC.
	/// </summary>
	public DateTime DueDate
	{
		get => _dueDate;
		set => _dueDate = value.Kind == DateTimeKind.Utc
			? value
			: DateTime.SpecifyKind(value, DateTimeKind.Utc);
	}

	public string? Feedback { get; set; }
	public string? Remarks { get; set; }

	public SubmissionStatus Status { get; set; }

	public decimal? Marks { get; set; }
	public decimal? TotalMarks { get; set; }

	public string? SubmittedDocumentUrl { get; set; }
	public string? OriginalFileName { get; set; }
	public string? SubmittedContent { get; set; }
	public string? MobileAttachmentsJson { get; private set; }

	public void SaveMobileResponse(string? text, string? attachmentsJson, string? firstBlob, string? firstName, bool draft, DateTimeOffset now, string? html = null, bool acknowledged = false)
	{
		if (draft && SubmissionDate.HasValue) throw new Volo.Abp.BusinessException(ShikshaMitra.Academic.Mobile.MobileSubmissionErrorCodes.DraftAfterSubmission);
		// Keep the API value identical before and after the database's microsecond precision round trip.
		var utc = now.ToUniversalTime();
		now = new DateTimeOffset(utc.Ticks - utc.Ticks % 10, TimeSpan.Zero);
		SubmittedContent = text;
		SubmittedContentHtml = html;
		Acknowledged = acknowledged;
		MobileUpdatedAt = now;
		if (draft) MobileSavedAt = now;
		MobileAttachmentsJson = attachmentsJson;
		SubmittedDocumentUrl = firstBlob;
		OriginalFileName = firstName;
		Status = draft ? SubmissionStatus.Draft : now.UtcDateTime > DueDate ? SubmissionStatus.Late : SubmissionStatus.Submitted;
		if (!draft) SubmissionDate ??= now;
	}

	public Guid? ReviewedBy { get; set; }
	public DateTimeOffset? ReviewedAt { get; set; }

	// Legacy field for backward compatibility
	public Guid? GradedBy { get; set; }
	public DateTimeOffset? GradedDate { get; set; }

	// Helper properties
	public bool IsAssignmentSubmission => AssignmentId.HasValue;
	public bool IsHomeworkSubmission => !AssignmentId.HasValue;

	// Private constructor for EF Core
	private StudentSubmissionEntity() { }

	// Constructor for creating homework submissions (backward compatibility)
	public StudentSubmissionEntity(
		Guid id,
		Guid homeworkAssignmentId,
		Guid studentId,
		Guid divisionId,
		DateTime dueDate,
		Guid? tenantId = null
	)
		: base(id)
	{
		HomeworkAssignmentId = homeworkAssignmentId;
		AssignmentId = null;
		StudentId = studentId;
		DivisionId = divisionId;
		DueDate = dueDate;
		TenantId = tenantId;
		Status = SubmissionStatus.NeedsReview;
	}

	// Constructor for creating assignment submissions (new)
	public StudentSubmissionEntity(
		Guid id,
		Guid assignmentId,
		Guid studentId,
		Guid divisionId,
		DateTime dueDate,
		Guid? tenantId,
		bool isAssignment
	)
		: base(id)
	{
		if (!isAssignment)
		{
			throw new ArgumentException("Use the other constructor for homework submissions");
		}

		HomeworkAssignmentId = null; // Null for assignment submissions
		AssignmentId = assignmentId;
		StudentId = studentId;
		DivisionId = divisionId;
		DueDate = dueDate;
		TenantId = tenantId;
		Status = SubmissionStatus.NeedsReview;
	}

	/// <summary>
	/// Updates feedback for the submission
	/// </summary>
	public void UpdateFeedback(
		decimal? marks,
		decimal? totalMarks,
		string? feedback,
		string? remarks,
		SubmissionStatus status,
		Guid reviewedBy
	)
	{
		if (marks.HasValue && totalMarks.HasValue && marks.Value > totalMarks.Value)
		{
			throw new ArgumentException("Marks cannot exceed total marks.");
		}

		Marks = marks;
		TotalMarks = totalMarks;
		Feedback = feedback;
		Remarks = remarks;
		Status = status;
		ReviewedBy = reviewedBy;
		ReviewedAt = DateTimeOffset.UtcNow;

		// Update legacy fields for backward compatibility
		GradedBy = reviewedBy;
		GradedDate = DateTimeOffset.UtcNow;
	}

	/// <summary>
	/// Updates only the submission status
	/// </summary>
	public void UpdateStatus(SubmissionStatus status)
	{
		Status = status;
	}

	/// <summary>
	/// Records student submission
	/// </summary>
	public void RecordSubmission(string? submittedContent, string? documentUrl = null)
	{
		SubmittedContent = submittedContent;
		SubmittedContentHtml = null;
		SubmittedDocumentUrl = documentUrl;
		SubmissionDate = DateTimeOffset.UtcNow;

		// Determine if submission is late
		// Compare UTC times: convert DueDate to DateTimeOffset for accurate comparison
		var dueDateOffset = new DateTimeOffset(DueDate, TimeSpan.Zero);
		if (DateTimeOffset.UtcNow > dueDateOffset)
		{
			Status = SubmissionStatus.Late;
		}
		else
		{
			Status = SubmissionStatus.Submitted;
		}
	}
}
