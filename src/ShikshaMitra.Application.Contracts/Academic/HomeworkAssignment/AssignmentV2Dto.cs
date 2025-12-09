using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Academic.HomeworkAssignment;

/// <summary>
/// DTO for assignment response
/// </summary>
public class AssignmentV2Dto : AuditedEntityDto<Guid>
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

	public int SubmissionType { get; set; }
	public string SubmissionTypeName { get; set; } = string.Empty;

	public List<AttachmentDto>? Attachments { get; set; }

	public List<AssignmentDivisionScheduleDto> Divisions { get; set; } = new();

	public CreatedByDto CreatedBy { get; set; } = null!;

	public DateTime? PublishedAt { get; set; } // Earliest publish date across divisions
	public string Status { get; set; } = string.Empty; // Draft / Partially Published / Published
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
