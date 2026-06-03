using System;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.ProfessionalManagement;

public class PerformanceOutcomeManager(
	IGuidGenerator guidGenerator,
	ICurrentTenant currentTenant)
	: DomainService
{
	public virtual PerformanceOutcomeSaveResult SaveFinalOutcome(
		PerformanceReviewAssignment assignment,
		PerformanceOutcome? existingOutcome,
		decimal finalScore,
		string finalRating,
		string? strengths,
		string? improvementAreas,
		PerformanceRecommendationType recommendation,
		string? recommendationNotes,
		bool incrementRecommended,
		bool promotionRecommended,
		DateTime finalizedAt)
	{
		var outcome = existingOutcome ?? new PerformanceOutcome(
			guidGenerator.Create(),
			currentTenant.Id,
			assignment.Id,
			finalScore,
			finalRating,
			strengths,
			improvementAreas,
			recommendation,
			recommendationNotes,
			incrementRecommended,
			promotionRecommended);

		if (existingOutcome != null)
		{
			existingOutcome.Update(
				finalScore,
				finalRating,
				strengths,
				improvementAreas,
				recommendation,
				recommendationNotes,
				incrementRecommended,
				promotionRecommended);
		}

		assignment.FinalizeReview(finalScore, finalRating, finalizedAt);
		return new PerformanceOutcomeSaveResult(outcome, existingOutcome == null);
	}
}
