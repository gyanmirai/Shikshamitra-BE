using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Academic.Curriculum;

public class CopySubjectAssignmentsDto
{
	[Required]
	public Guid SourceClassId { get; set; }

	[Required]
	public Guid TargetClassId { get; set; }

	public List<Guid>? SubjectIds { get; set; }

	public bool IncludeTeachers { get; set; }

	public bool IncludeCurriculumDetails { get; set; } = true;

	public bool IncludeDocuments { get; set; } = true;

	public string ConflictStrategy { get; set; } = "SKIP";

	public SubjectStatus TargetStatus { get; set; } = SubjectStatus.Draft;
}

public class CopySubjectAssignmentsResultDto
{
	public int CopiedCount { get; set; }
	public int SkippedCount { get; set; }
	public int OverwrittenCount { get; set; }
	public string Message { get; set; } = string.Empty;
}
