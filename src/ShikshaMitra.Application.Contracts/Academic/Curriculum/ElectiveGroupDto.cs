using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Academic.Curriculum;

/// <summary>
/// DTO for retrieving elective group details with associated subjects
/// </summary>
public class ElectiveGroupDto : EntityDto<Guid>
{
	/// <summary>
	/// Name of the elective group
	/// </summary>
	public required string Name { get; set; }

	/// <summary>
	/// Hex color code for UI display (e.g., #FF5722)
	/// </summary>
	public required string Color { get; set; }

	public string? GroupType { get; set; }

	public string? Description { get; set; }

	public SubjectMasterStatus Status { get; set; }

	public int? SortOrder { get; set; }

	public string? ArchiveReason { get; set; }

	/// <summary>
	/// Education medium this elective group belongs to
	/// </summary>
	public Guid EducationMediumId { get; set; }

	/// <summary>
	/// List of subjects included in this elective group
	/// </summary>
	public List<SubjectDto> Subjects { get; set; } = new List<SubjectDto>();

	public int UsedInClassesCount { get; set; }
	public int StudentSelectionsCount { get; set; }
	public int TimetablePeriodsCount { get; set; }
	public int ExamSchedulesCount { get; set; }
	public bool IsUsed { get; set; }
	public bool CanDelete { get; set; }
	public DateTime? LastUpdatedAt { get; set; }
	public string? LastUpdatedBy { get; set; }
}

public class ElectiveGroupStatusChangeDto
{
	public string? Reason { get; set; }
}

public class ElectiveGroupDuplicateCheckDto
{
	[System.ComponentModel.DataAnnotations.Required]
	public required string Name { get; set; }
	public Guid? ExcludeElectiveGroupId { get; set; }
	public List<Guid> SubjectIds { get; set; } = [];
}

public class ElectiveGroupDuplicateMatchDto
{
	public Guid Id { get; set; }
	public required string Name { get; set; }
	public SubjectMasterStatus Status { get; set; }
	public string MatchType { get; set; } = "Exact";
}

public class ElectiveGroupDuplicateCheckResultDto
{
	public bool HasExactDuplicate { get; set; }
	public bool HasSimilarDuplicate { get; set; }
	public bool HasSameSubjectCombination { get; set; }
	public List<ElectiveGroupDuplicateMatchDto> Matches { get; set; } = [];
}

public class ElectiveGroupUsageDetailDto
{
	public Guid ElectiveGroupId { get; set; }
	public required string ElectiveGroupName { get; set; }
	public bool CanDelete { get; set; }
	public List<string> SafeActions { get; set; } = [];
	public List<string> Subjects { get; set; } = [];
	public ElectiveGroupUsageSummaryDto Usage { get; set; } = new();
}

public class ElectiveGroupUsageSummaryDto
{
	public int ClassAllocationsCount { get; set; }
	public int StudentSelectionsCount { get; set; }
	public int TimetablePeriodsCount { get; set; }
	public int ExamSchedulesCount { get; set; }
	public List<ElectiveGroupSubjectStudentCountDto> SubjectWiseStudentCount { get; set; } = [];
}

public class ElectiveGroupSubjectStudentCountDto
{
	public required string Subject { get; set; }
	public int Count { get; set; }
}

public class ElectiveGroupAuditLogDto
{
	public DateTime? ChangedDate { get; set; }
	public string? ChangedBy { get; set; }
	public required string Action { get; set; }
	public string? BeforeValue { get; set; }
	public string? AfterValue { get; set; }
	public string? Reason { get; set; }
}
