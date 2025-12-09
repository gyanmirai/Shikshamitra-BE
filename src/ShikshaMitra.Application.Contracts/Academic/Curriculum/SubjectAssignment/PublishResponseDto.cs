using System;

namespace ShikshaMitra.Academic.Curriculum;

/// <summary>
/// Response DTO for publish operation
/// </summary>
public class PublishResponseDto
{
	/// <summary>
	/// Assignment ID
	/// </summary>
	public Guid Id { get; set; }

	/// <summary>
	/// New status after publish (1 = Published)
	/// </summary>
	public int Status { get; set; }

	/// <summary>
	/// When the assignment was published
	/// </summary>
	public DateTime PublishedAt { get; set; }
}
