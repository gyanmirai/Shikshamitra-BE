using System;

namespace ShikshaMitra.Students.Attendance;

/// <summary>
/// DTO for downloading student leave attachment
/// </summary>
public class StudentLeaveAttachmentDto
{
	public byte[] FileContent { get; set; } = Array.Empty<byte>();
	public string FileName { get; set; } = string.Empty;
	public string ContentType { get; set; } = string.Empty;
}
