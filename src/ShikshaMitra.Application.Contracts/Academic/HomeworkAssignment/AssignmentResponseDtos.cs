using System;
using System.Collections.Generic;

namespace ShikshaMitra.Academic.HomeworkAssignment;

/// <summary>
/// Response DTO for assignment creation
/// </summary>
public class AssignmentCreateResponseDto
{
	public string Message { get; set; } = string.Empty;
	public AssignmentDto Data { get; set; } = null!;
}

/// <summary>
/// Response DTO for assignment list
/// </summary>
public class AssignmentListResponseDto
{
	public List<AssignmentDto> Data { get; set; } = new();
}

/// <summary>
/// Response DTO for single assignment get
/// </summary>
public class AssignmentGetResponseDto
{
	public AssignmentDto Data { get; set; } = null!;
}

/// <summary>
/// Response DTO for assignment publish
/// </summary>
public class AssignmentPublishResponseDto
{
	public string Message { get; set; } = string.Empty;
	public PublishDataDto Data { get; set; } = null!;
}

/// <summary>
/// Publish data DTO
/// </summary>
public class PublishDataDto
{
	public Guid Id { get; set; }
	public DateTime PublishedAt { get; set; }
}

/// <summary>
/// DTO for assignment filters
/// </summary>
public class GetAssignmentFilterDto
{
	public Guid? ClassId { get; set; }
	public Guid? SubjectId { get; set; }
	public List<Guid>? ChapterIds { get; set; }
	public string? Status { get; set; } // "draft" | "published" | "partially_published"
}
