using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Academic.Curriculum;

public class SubjectDuplicateCheckDto
{
	[Required]
	public required string Name { get; set; }
	public Guid? ExcludeSubjectId { get; set; }
}

public class SubjectDuplicateMatchDto
{
	public Guid Id { get; set; }
	public required string Name { get; set; }
	public string? DisplayName { get; set; }
	public SubjectMasterStatus Status { get; set; }
	public string MatchType { get; set; } = "Exact";
}

public class SubjectDuplicateCheckResultDto
{
	public bool HasExactDuplicate { get; set; }
	public bool HasSimilarDuplicate { get; set; }
	public List<SubjectDuplicateMatchDto> Matches { get; set; } = [];
}

public class SubjectStatusChangeDto
{
	public string? Reason { get; set; }
}

public class BulkCreateSubjectsDto
{
	public List<CreateUpdateSubjectDto> Subjects { get; set; } = [];
}

public class SubjectUsageDetailDto
{
	public Guid SubjectId { get; set; }
	public required string SubjectName { get; set; }
	public bool CanDelete { get; set; }
	public List<string> SafeActions { get; set; } = [];
	public SubjectUsageSummaryDto Usage { get; set; } = new();
}

public class SubjectUsageSummaryDto
{
	public List<SubjectAllocationUsageDto> SubjectAllocations { get; set; } = [];
	public List<string> ElectiveGroups { get; set; } = [];
	public int ChaptersCount { get; set; }
	public int TimetablePeriodsCount { get; set; }
	public int ExamSchedulesCount { get; set; }
	public int LessonPlansCount { get; set; }
	public int HomeworkCount { get; set; }
	public int TeacherMappingsCount { get; set; }
}

public class SubjectAllocationUsageDto
{
	public string? Standard { get; set; }
	public string? Code { get; set; }
}

public class SubjectAuditLogDto
{
	public DateTime? ChangedDate { get; set; }
	public string? ChangedBy { get; set; }
	public required string Action { get; set; }
	public string? BeforeValue { get; set; }
	public string? AfterValue { get; set; }
	public string? Reason { get; set; }
}
