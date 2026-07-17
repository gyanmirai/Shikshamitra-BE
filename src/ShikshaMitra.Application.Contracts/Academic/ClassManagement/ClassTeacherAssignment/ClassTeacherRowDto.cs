using System;
using System.Collections.Generic;

namespace ShikshaMitra.Academic.ClassManagement.ClassTeacherAssignment;

public class ClassTeacherRowDto
{
	public Guid ClassId { get; set; }
	public string ClassName { get; set; } = string.Empty;
	public Guid DivisionId { get; set; }
	public string DivisionName { get; set; } = string.Empty;
	public int StudentCount { get; set; }
	public string AssignmentStatus { get; set; } = "Unassigned";
	public string ApprovalStatus { get; set; } = "Not Submitted";
	public string AcknowledgmentStatus { get; set; } = "Not Sent";
	public string HandoverStatus { get; set; } = "Not Required";
	public string AssignmentType { get; set; } = "Permanent";
	public DateTime? EffectiveFrom { get; set; }
	public DateTime? EffectiveTo { get; set; }
	public DateTime? LastUpdatedAt { get; set; }
	public string? LastUpdatedBy { get; set; }
	public int ConflictCount { get; set; }
	public List<ClassTeacherValidationIssueDto> Issues { get; set; } = new();
	public AssignedTeacherDto? AssignedTeacher { get; set; }
}

public class AssignedTeacherDto
{
	public Guid TeacherId { get; set; }
	public string TeacherName { get; set; } = string.Empty;
	public string? EmployeeCode { get; set; }
	public string? Department { get; set; }
	public bool IsActive { get; set; }
	public int CurrentClassTeacherCount { get; set; }
	public int MaximumClassTeacherCount { get; set; }
	public string AvailabilityStatus { get; set; } = "Available";
	public string WorkloadStatus { get; set; } = "Normal";
	public string ConflictStatus { get; set; } = "None";
	public int RecommendationScore { get; set; }
}

public class ClassTeacherSummaryDto
{
	public int TotalClasses { get; set; }
	public int Assigned { get; set; }
	public int Unassigned { get; set; }
	public int Conflicts { get; set; }
	public int PendingChanges { get; set; }
	public int PendingApproval { get; set; }
	public int TemporaryAssignments { get; set; }
	public int AcknowledgmentPending { get; set; }
	public string Status { get; set; } = "Unassigned";
	public List<string> Reasons { get; set; } = new();
}

public class ClassTeacherValidationIssueDto
{
	public string Severity { get; set; } = "Info";
	public string Code { get; set; } = string.Empty;
	public string Message { get; set; } = string.Empty;
	public Guid? ClassId { get; set; }
	public Guid? DivisionId { get; set; }
	public Guid? TeacherId { get; set; }
	public string? SuggestedFix { get; set; }
}

public class ClassTeacherValidationResultDto
{
	public bool CanConfirm { get; set; }
	public ClassTeacherSummaryDto Summary { get; set; } = new();
	public List<ClassTeacherValidationIssueDto> Issues { get; set; } = new();
}

public class ClassTeacherEligibleTeacherDto
{
	public Guid TeacherId { get; set; }
	public string TeacherName { get; set; } = string.Empty;
	public string? EmployeeCode { get; set; }
	public string? Department { get; set; }
	public int CurrentWorkload { get; set; }
	public int MaximumWorkload { get; set; } = 30;
	public int CurrentClassTeacherCount { get; set; }
	public int MaximumClassTeacherCount { get; set; }
	public bool IsActive { get; set; }
	public bool IsEligible { get; set; }
	public bool HasTimetableConflict { get; set; }
	public bool IsOnLeave { get; set; }
	public string AvailabilityStatus { get; set; } = "Available";
	public string WorkloadStatus { get; set; } = "Normal";
	public string ConflictStatus { get; set; } = "None";
	public int RecommendationScore { get; set; }
	public List<string> IneligibilityReasons { get; set; } = new();
}

public class ClassTeacherAssignmentHistoryDto
{
	public Guid ClassId { get; set; }
	public string ClassName { get; set; } = string.Empty;
	public Guid DivisionId { get; set; }
	public string DivisionName { get; set; } = string.Empty;
	public Guid? PreviousTeacherId { get; set; }
	public string? PreviousTeacherName { get; set; }
	public Guid? NewTeacherId { get; set; }
	public string? NewTeacherName { get; set; }
	public DateTime ChangedAt { get; set; }
	public string ChangedBy { get; set; } = "System";
	public string ChangeReason { get; set; } = "Current assignment";
}
