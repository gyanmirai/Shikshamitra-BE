using System.Collections.Generic;

namespace ShikshaMitra.Academic.HomeworkAssignment;

/// <summary>
/// Response wrapper for assignment type creation
/// </summary>
public class AssignmentTypeCreateResponseDto
{
	public string Message { get; set; } = string.Empty;
	public AssignmentTypeDto Data { get; set; } = null!;
}

/// <summary>
/// Response wrapper for assignment type list
/// </summary>
public class AssignmentTypeListResponseDto
{
	public List<AssignmentTypeDto> Data { get; set; } = new();
}

/// <summary>
/// Response wrapper for single assignment type
/// </summary>
public class AssignmentTypeGetResponseDto
{
	public AssignmentTypeDto Data { get; set; } = null!;
}
