using System;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public record SubmissionAttachmentDto(Guid? Id, string Name, string Url, string? ContentType, long? Size);
