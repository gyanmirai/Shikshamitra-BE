using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionInquiryCounsellorDto
{
	public Guid Id { get; set; }
	public string UserName { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
}

public class AdmissionInquiryManagementDto
{
	public Guid InquiryId { get; set; }
	public bool Managed { get; set; }
	public AdmissionInquiryPolicy? Policy { get; set; }
	public string ConcurrencyStamp { get; set; } = string.Empty;
	public Guid? AssignedCounsellorId { get; set; }
	public DateTime? FirstContactDueAt { get; set; }
	public DateTime? QualificationDueAt { get; set; }
	public DateTime? LastContactAt { get; set; }
	public int ContactCount { get; set; }
	public AdmissionInquiryQualificationOutcome? Outcome { get; set; }
	public string? OutcomeReason { get; set; }
	public DateTime? OutcomeAt { get; set; }
	public Guid? OutcomeBy { get; set; }
	public bool IsOverdue { get; set; }
	public bool IsLocked { get; set; }
	public bool CanStartApplication { get; set; }
	public List<string> Blockers { get; set; } = [];
	public List<AdmissionInquiryQualificationAnswerDto> QualificationAnswers { get; set; } = [];
	public List<AdmissionInquiryFollowUpDto> FollowUps { get; set; } = [];
}

public class AdmissionInquiryQualificationAnswerDto
{
	[Required, MaxLength(64)] public string Code { get; set; } = string.Empty;
	public bool Passed { get; set; }
	[MaxLength(512)] public string? Notes { get; set; }
}

public class AdmissionInquiryFollowUpDto
{
	public Guid Id { get; set; }
	public Guid? AssignedCounsellorId { get; set; }
	public DateTime ScheduledAt { get; set; }
	public string Purpose { get; set; } = string.Empty;
	public AdmissionInquiryFollowUpStatus Status { get; set; }
	public DateTime? CompletedAt { get; set; }
	public Guid? CompletedBy { get; set; }
	public string? CompletionNote { get; set; }
	public string ConcurrencyStamp { get; set; } = string.Empty;
}

public class AdmissionInquiryManagementCommandDto
{
	[Required, MaxLength(40)] public string ConcurrencyStamp { get; set; } = string.Empty;
}

public class RecordAdmissionInquiryContactDto : AdmissionInquiryManagementCommandDto
{
	[Required, MaxLength(1024)] public string Note { get; set; } = string.Empty;
	public DateTimeOffset? ContactedAt { get; set; }
}

public class ScheduleAdmissionInquiryFollowUpDto : AdmissionInquiryManagementCommandDto
{
	public DateTimeOffset ScheduledAt { get; set; }
	[Required, MaxLength(512)] public string Purpose { get; set; } = string.Empty;
	public Guid? AssignedCounsellorId { get; set; }
}

public class CompleteAdmissionInquiryFollowUpDto : AdmissionInquiryManagementCommandDto
{
	[Required, MaxLength(40)] public string FollowUpConcurrencyStamp { get; set; } = string.Empty;
	[Required, MaxLength(1024)] public string Note { get; set; } = string.Empty;
}

public class CancelAdmissionInquiryFollowUpDto : CompleteAdmissionInquiryFollowUpDto { }

public class QualifyAdmissionInquiryDto : AdmissionInquiryManagementCommandDto
{
	public AdmissionInquiryQualificationOutcome Outcome { get; set; }
	[Required, MaxLength(1024)] public string Reason { get; set; } = string.Empty;
	public Guid? DuplicateOfId { get; set; }
	public List<AdmissionInquiryQualificationAnswerDto> Answers { get; set; } = [];
}

public class ReopenAdmissionInquiryDto : AdmissionInquiryManagementCommandDto
{
	[Required, MaxLength(1024)] public string Reason { get; set; } = string.Empty;
}
