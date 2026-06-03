using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.HR.Policies;

public class HrPolicyVersionDto : EntityDto<Guid>
{
	public string PolicyType { get; set; } = string.Empty;
	public Guid PolicyId { get; set; }
	public Guid AcademicYearId { get; set; }
	public int VersionNumber { get; set; }
	public string Name { get; set; } = string.Empty;
	public string Status { get; set; } = string.Empty;
	public DateTime? EffectiveFrom { get; set; }
	public DateTime? EffectiveTo { get; set; }
	public string ChangeAction { get; set; } = string.Empty;
	public string? ChangeReason { get; set; }
	public DateTime CreationTime { get; set; }
	public Guid? CreatorId { get; set; }
	public string? CreatedByName { get; set; }
	public string Summary { get; set; } = string.Empty;
	public int ChangedFieldCount { get; set; }
	public string SnapshotJson { get; set; } = "{}";
}

public class HrPolicyVersionCompareDto
{
	public string PolicyType { get; set; } = string.Empty;
	public Guid PolicyId { get; set; }
	public HrPolicyVersionDto FromVersion { get; set; } = new();
	public HrPolicyVersionDto ToVersion { get; set; } = new();
	public List<HrPolicyVersionDifferenceDto> Differences { get; set; } = [];
}

public class HrPolicyVersionDifferenceDto
{
	public string Field { get; set; } = string.Empty;
	public string? FromValue { get; set; }
	public string? ToValue { get; set; }
	public string FieldKey { get; set; } = string.Empty;
	public string FieldLabel { get; set; } = string.Empty;
	public string Category { get; set; } = string.Empty;
	public string FromDisplayValue { get; set; } = string.Empty;
	public string ToDisplayValue { get; set; } = string.Empty;
	public string? FromRawValue { get; set; }
	public string? ToRawValue { get; set; }
}

public class HrPolicyRestoreDraftRequestDto
{
	public string? Name { get; set; }
	public DateTime? EffectiveFrom { get; set; }
	public string? ChangeReason { get; set; }
}
