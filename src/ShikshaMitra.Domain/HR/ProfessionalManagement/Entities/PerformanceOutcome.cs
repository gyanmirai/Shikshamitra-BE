using System;
using ShikshaMitra.HR;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.ProfessionalManagement;

public class PerformanceOutcome : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid ReviewAssignmentId { get; private set; }
	public decimal FinalScore { get; private set; }
	public string FinalRating { get; private set; } = string.Empty;
	public string? Strengths { get; private set; }
	public string? ImprovementAreas { get; private set; }
	public PerformanceRecommendationType Recommendation { get; private set; }
	public string? RecommendationNotes { get; private set; }
	public bool IncrementRecommended { get; private set; }
	public bool PromotionRecommended { get; private set; }

	protected PerformanceOutcome()
	{
	}

	public PerformanceOutcome(Guid id, Guid? tenantId, Guid reviewAssignmentId, decimal finalScore, string finalRating, string? strengths, string? improvementAreas, PerformanceRecommendationType recommendation, string? recommendationNotes, bool incrementRecommended, bool promotionRecommended) : base(id)
	{
		TenantId = tenantId;
		ReviewAssignmentId = reviewAssignmentId;
		Update(finalScore, finalRating, strengths, improvementAreas, recommendation, recommendationNotes, incrementRecommended, promotionRecommended);
	}

	public void Update(decimal finalScore, string finalRating, string? strengths, string? improvementAreas, PerformanceRecommendationType recommendation, string? recommendationNotes, bool incrementRecommended, bool promotionRecommended)
	{
		FinalScore = Math.Round(finalScore, 2);
		FinalRating = finalRating.Trim();
		Strengths = string.IsNullOrWhiteSpace(strengths) ? null : strengths.Trim();
		ImprovementAreas = string.IsNullOrWhiteSpace(improvementAreas) ? null : improvementAreas.Trim();
		Recommendation = recommendation;
		RecommendationNotes = string.IsNullOrWhiteSpace(recommendationNotes) ? null : recommendationNotes.Trim();
		IncrementRecommended = incrementRecommended;
		PromotionRecommended = promotionRecommended;
	}
}
