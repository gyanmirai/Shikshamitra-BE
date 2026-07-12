using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmDataQualityIssueDto : EntityDto<Guid>
{
	public Guid AcademicYearId { get; set; }
	public AdmissionCrmDataQualityIssueType IssueType { get; set; }
	public AdmissionCrmDataQualityIssueSeverity Severity { get; set; }
	public AdmissionCrmDataQualityIssueStatus Status { get; set; }
	public Guid? LeadId { get; set; }
	public Guid? StudentId { get; set; }
	public string Title { get; set; } = string.Empty;
	public IReadOnlyDictionary<string, string?> Details { get; set; } = new Dictionary<string, string?>();
	public IReadOnlyList<string> ImpactedReports { get; set; } = [];
	public DateTime FirstDetectedAt { get; set; }
	public DateTime LastDetectedAt { get; set; }
	public Guid? OwnerUserId { get; set; }
	public string? ResolutionReason { get; set; }
	public DateTime? ResolvedAt { get; set; }
}

public class GetAdmissionCrmDataQualityIssuesInput : PagedAndSortedResultRequestDto
{
	public AdmissionCrmDataQualityIssueStatus? Status { get; set; }
	public AdmissionCrmDataQualityIssueSeverity? MinimumSeverity { get; set; }
	public AdmissionCrmDataQualityIssueType? IssueType { get; set; }
	public Guid? LeadId { get; set; }
}

public class AdmissionCrmReconciliationResultDto
{
	public Guid AcademicYearId { get; set; }
	public DateTime DataAsOf { get; set; }
	public int DetectedCount { get; set; }
	public int NewCount { get; set; }
	public int ReopenedCount { get; set; }
	public int AutoResolvedCount { get; set; }
	public IReadOnlyDictionary<string, int> CountsByType { get; set; } = new Dictionary<string, int>();
}

public class AcknowledgeAdmissionCrmDataQualityIssueDto
{
	public Guid? OwnerUserId { get; set; }
}

public class ResolveAdmissionCrmDataQualityIssueDto
{
	public bool Ignore { get; set; }
	public string Reason { get; set; } = string.Empty;
}
