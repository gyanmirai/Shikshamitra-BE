using System;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public class AttachmentInfoDto
{
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public DateTimeOffset? UploadedAt { get; set; }
}
