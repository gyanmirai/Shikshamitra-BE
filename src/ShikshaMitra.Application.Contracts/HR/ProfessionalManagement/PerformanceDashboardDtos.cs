using System;

namespace ShikshaMitra.HR.ProfessionalManagement;

public class PerformanceDashboardDto
{
	public int ActiveCycles { get; set; }
	public int PendingSelfReviews { get; set; }
	public int PendingReviewerReviews { get; set; }
	public int OverdueReviews { get; set; }
	public int FinalizedReviews { get; set; }
	public int TrainingRecommendations { get; set; }
	public int IncrementRecommendations { get; set; }
}

public class RatingDistributionDto
{
	public string Rating { get; set; } = string.Empty;
	public int Count { get; set; }
}

public class PendingActionDto
{
	public Guid ReviewId { get; set; }
	public string EmployeeName { get; set; } = string.Empty;
	public string Action { get; set; } = string.Empty;
	public DateTime? DueDate { get; set; }
}

public class PerformanceSqaaEvidenceDto
{
	public PerformanceSqaaDomain Domain { get; set; }
	public int TemplateCount { get; set; }
	public int AssignedReviewCount { get; set; }
	public int MappedCriteria { get; set; }
	public int EvidenceRequiredCriteria { get; set; }
	public int EvidenceSubmitted { get; set; }
	public int MissingEvidence { get; set; }
}

public class PerformanceAuditEventDto
{
	public Guid Id { get; set; }
	public Guid? ReviewAssignmentId { get; set; }
	public string EventType { get; set; } = string.Empty;
	public string Summary { get; set; } = string.Empty;
	public string? MetadataJson { get; set; }
	public DateTime CreationTime { get; set; }
}
