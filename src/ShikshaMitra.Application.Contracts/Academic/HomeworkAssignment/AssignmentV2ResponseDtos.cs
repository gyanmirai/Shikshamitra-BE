using System;
using System.Collections.Generic;

namespace ShikshaMitra.Academic.HomeworkAssignment;

/// <summary>
/// Response DTO for assignment creation
/// </summary>
public class AssignmentV2CreateResponseDto
{
	public string Message { get; set; } = string.Empty;
	public AssignmentV2Dto Data { get; set; } = null!;
}

/// <summary>
/// Response DTO for assignment list
/// </summary>
public class AssignmentV2ListResponseDto
{
	public List<AssignmentV2Dto> Data { get; set; } = new();
}

/// <summary>
/// Response DTO for single assignment get
/// </summary>
public class AssignmentV2GetResponseDto
{
	public AssignmentV2Dto Data { get; set; } = null!;
}

/// <summary>
/// Response DTO for assignment publish
/// </summary>
public class AssignmentV2PublishResponseDto
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
