using System;
using System.Collections.Generic;

namespace ShikshaMitra.HR.Policies;

public class HrPolicyAuditSummaryDto
{
	public string PolicyType { get; set; } = string.Empty;
	public Guid PolicyId { get; set; }
	public string Name { get; set; } = string.Empty;
	public int Version { get; set; }
	public DateTime CreationTime { get; set; }
	public Guid? CreatorId { get; set; }
	public string? CreatedByName { get; set; }
	public DateTime? LastModificationTime { get; set; }
	public Guid? LastModifierId { get; set; }
	public string? LastModifiedByName { get; set; }
	public string? LastDefaultSetSummary { get; set; }
	public string? LatestAssignmentReason { get; set; }
	public List<HrPolicyAuditEventDto> Events { get; set; } = [];
}

public class HrPolicyAuditEventDto
{
	public DateTime Timestamp { get; set; }
	public Guid? ActorId { get; set; }
	public string? ActorName { get; set; }
	public string Action { get; set; } = string.Empty;
	public string? SourceMethod { get; set; }
	public string? ChangedFieldsSummary { get; set; }
}
