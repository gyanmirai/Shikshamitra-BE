using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Academic.HomeworkAssignment;

/// <summary>
/// DTO for assignment response
/// </summary>
public class AssignmentDto : AuditedEntityDto<Guid>
{
	public Guid ClassId { get; set; }
	public string ClassName { get; set; } = string.Empty;

	public Guid SubjectId { get; set; }
	public string SubjectName { get; set; } = string.Empty;

	public List<ChapterInfoDto>? Chapters { get; set; }

	public string Title { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;

	public Guid AssignmentTypeId { get; set; }
	public string AssignmentTypeName { get; set; } = string.Empty;
	public int? DefaultSubmissionType { get; set; }
	public decimal? DefaultMarks { get; set; }
	public int? DefaultDueDays { get; set; }
	public bool EvaluationRequired { get; set; } = true;
	public int? DefaultEvaluationMethod { get; set; }
	public int? AssignmentTypeStatus { get; set; }

	public int SubmissionType { get; set; }
	public string SubmissionTypeName { get; set; } = string.Empty;

	public List<AttachmentDto>? Attachments { get; set; }

	public List<AssignmentDivisionScheduleDto> Divisions { get; set; } = new();

	public CreatedByDto CreatedBy { get; set; } = null!;

	public DateTime? PublishedAt { get; set; } // Earliest publish date across divisions
	public string Status { get; set; } = string.Empty; // Draft / Partially Published / Published
	public int TotalStudents { get; set; }
	public int SubmittedCount { get; set; }
	public int PendingCount { get; set; }
	public int LateCount { get; set; }
	public int CheckedCount { get; set; }
	public int EvaluationPendingCount { get; set; }
}

/// <summary>
/// DTO for assignment division schedule response
/// </summary>
public class AssignmentDivisionScheduleDto
{
	public Guid DivisionId { get; set; }
	public string DivisionName { get; set; } = string.Empty;
	public DateTime? PublishDate { get; set; }
	public DateTime DueDate { get; set; }
	public DateTime? PublishedAt { get; set; }
	public bool IsPublished { get; set; }
}

/// <summary>
/// DTO for chapter information
/// </summary>
public class ChapterInfoDto
{
	public Guid ChapterId { get; set; }
	public string ChapterName { get; set; } = string.Empty;
}

/// <summary>
/// DTO for attachment information
/// </summary>
public class AttachmentDto
{
	public string Name { get; set; } = string.Empty;
	public string Url { get; set; } = string.Empty;
	public long Size { get; set; }
}

/// <summary>
/// DTO for created by information
/// </summary>
public class CreatedByDto
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
}
