using System;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public sealed record AssignmentDivisionPublished(Guid? TenantId, Guid ScheduleId, DateTime PublishedAt);
