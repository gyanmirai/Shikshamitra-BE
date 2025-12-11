using System.Collections.Generic;

namespace ShikshaMitra.Academic.HomeworkAssignment;

/// <summary>
/// Response wrapper for homework type creation
/// </summary>
public class HomeworkTypeCreateResponseDto
{
	public string Message { get; set; } = string.Empty;
	public HomeworkTypeDto Data { get; set; } = null!;
}

/// <summary>
/// Response wrapper for homework type list
/// </summary>
public class HomeworkTypeListResponseDto
{
	public List<HomeworkTypeDto> Data { get; set; } = new();
}

/// <summary>
/// Response wrapper for single homework type
/// </summary>
public class HomeworkTypeGetResponseDto
{
	public HomeworkTypeDto Data { get; set; } = null!;
}

/// <summary>
/// Generic message response for update/delete operations
/// </summary>
public class MessageResponseDto
{
	public string Message { get; set; } = string.Empty;
}
