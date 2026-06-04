using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.HR.ProfessionalManagement;

public class ProfessionalManagementDashboardReader(
	IRepository<ProfessionalReviewCycle, Guid> cycleRepository,
	IRepository<PerformanceReviewAssignment, Guid> assignmentRepository,
	IRepository<PerformanceReviewStage, Guid> stageRepository,
	IRepository<PerformanceOutcome, Guid> outcomeRepository,
	IRepository<DevelopmentPlan, Guid> developmentPlanRepository,
	ProfessionalManagementDashboardLookupReader lookupReader,
	IAsyncQueryableExecuter asyncExecuter)
	: ITransientDependency
{
	public async Task<PerformanceDashboardDto> GetDashboardAsync(DateTime today)
	{
		var cycleQuery = await cycleRepository.GetQueryableAsync();
		var assignmentQuery = await assignmentRepository.GetQueryableAsync();
		var stageQuery = await stageRepository.GetQueryableAsync();
		var outcomeQuery = await outcomeRepository.GetQueryableAsync();
		var cycles = await asyncExecuter.ToListAsync(cycleQuery.Where(x =>
			x.Status == ProfessionalReviewCycleStatus.Published ||
			x.Status == ProfessionalReviewCycleStatus.InProgress ||
			x.Status == ProfessionalReviewCycleStatus.Calibration));
		var activeCycleIds = cycles.Select(x => x.Id).ToHashSet();
		var assignments = await asyncExecuter.ToListAsync(assignmentQuery.Where(x =>
			activeCycleIds.Contains(x.CycleId) &&
			x.Status != ProfessionalReviewStatus.Finalized));
		var activeAssignmentIds = assignments.Select(x => x.Id).ToHashSet();
		var stages = await asyncExecuter.ToListAsync(stageQuery.Where(x =>
			activeAssignmentIds.Contains(x.ReviewAssignmentId) &&
			x.Status != PerformanceReviewStageStatus.Submitted));

		return new PerformanceDashboardDto
		{
			ActiveCycles = cycles.Count,
			PendingSelfReviews = stages.Count(x => x.StageType == PerformanceReviewStageType.SelfAppraisal),
			PendingReviewerReviews = stages.Count(x => x.StageType != PerformanceReviewStageType.SelfAppraisal),
			OverdueReviews = ProfessionalManagementRules.CountOverdueReviews(cycles, assignments, stages, today),
			FinalizedReviews = await asyncExecuter.CountAsync(assignmentQuery.Where(x => x.Status == ProfessionalReviewStatus.Finalized)),
			TrainingRecommendations = await asyncExecuter.CountAsync(outcomeQuery.Where(x => x.Recommendation == PerformanceRecommendationType.Training)),
			IncrementRecommendations = await asyncExecuter.CountAsync(outcomeQuery.Where(x =>
				x.IncrementRecommended ||
				x.Recommendation == PerformanceRecommendationType.Increment))
		};
	}

	public async Task<List<RatingDistributionDto>> GetRatingDistributionAsync(Guid? cycleId)
	{
		var query = await assignmentRepository.GetQueryableAsync();
		if (cycleId.HasValue)
		{
			query = query.Where(x => x.CycleId == cycleId);
		}

		var rows = await asyncExecuter.ToListAsync(query.Where(x => x.FinalRating != null));
		return rows
			.GroupBy(x => x.FinalRating!)
			.Select(x => new RatingDistributionDto { Rating = x.Key, Count = x.Count() })
			.OrderBy(x => x.Rating)
			.ToList();
	}

	public async Task<List<PendingActionDto>> GetPendingActionsAsync(Guid? cycleId)
	{
		var reviews = (await lookupReader.GetReviewsAsync(cycleId, null, null, null))
			.Where(x => x.Status != ProfessionalReviewStatus.Finalized)
			.Take(100)
			.ToList();
		var cycles = await lookupReader.GetCyclesByIdsAsync(reviews.Select(x => x.CycleId).Distinct().ToList());
		var stageQuery = await stageRepository.GetQueryableAsync();
		var reviewIds = reviews.Select(x => x.Id).ToList();
		var stages = await asyncExecuter.ToListAsync(stageQuery.Where(x =>
			reviewIds.Contains(x.ReviewAssignmentId) &&
			x.Status != PerformanceReviewStageStatus.Submitted));
		var stagesByReview = stages
			.GroupBy(x => x.ReviewAssignmentId)
			.ToDictionary(x => x.Key, x => x.OrderBy(stage => stage.StageType).First());

		return reviews.Select(x =>
		{
			stagesByReview.TryGetValue(x.Id, out var pendingStage);
			var dueDate = pendingStage?.StageType switch
			{
				PerformanceReviewStageType.SelfAppraisal => cycles.GetValueOrDefault(x.CycleId)?.SelfReviewDueDate,
				PerformanceReviewStageType.HrVerification or PerformanceReviewStageType.PrincipalManagement => cycles.GetValueOrDefault(x.CycleId)?.FinalizationDueDate,
				_ => cycles.GetValueOrDefault(x.CycleId)?.ReviewerDueDate
			};
			return new PendingActionDto
			{
				ReviewId = x.Id,
				EmployeeName = x.EmployeeName,
				Action = pendingStage == null ? "Finalize appraisal outcome" : $"{pendingStage.StageType} pending",
				DueDate = dueDate
			};
		}).ToList();
	}

	public async Task<List<DevelopmentPlanDto>> GetTrainingNeedsAsync(Guid? cycleId)
	{
		var query = await developmentPlanRepository.GetQueryableAsync();
		var rows = await asyncExecuter.ToListAsync(query
			.Where(x => x.Status != DevelopmentPlanStatus.Completed)
			.OrderBy(x => x.TargetDate));
		if (cycleId.HasValue)
		{
			var ids = (await lookupReader.GetAssignmentsForCycleAsync(cycleId.Value)).Select(x => x.Id).ToHashSet();
			rows = rows.Where(x => ids.Contains(x.ReviewAssignmentId)).ToList();
		}

		return rows.Select(ProfessionalManagementProjection.ToDevelopmentPlanDto).ToList();
	}

	public async Task<List<PerformanceOutcomeDto>> GetIncrementRecommendationsAsync(Guid? cycleId)
	{
		var query = await outcomeRepository.GetQueryableAsync();
		var outcomes = await asyncExecuter.ToListAsync(query.Where(x =>
			x.IncrementRecommended ||
			x.PromotionRecommended ||
			x.Recommendation == PerformanceRecommendationType.Increment ||
			x.Recommendation == PerformanceRecommendationType.Promotion));
		if (cycleId.HasValue)
		{
			var ids = (await lookupReader.GetAssignmentsForCycleAsync(cycleId.Value)).Select(x => x.Id).ToHashSet();
			outcomes = outcomes.Where(x => ids.Contains(x.ReviewAssignmentId)).ToList();
		}

		return outcomes.Select(ProfessionalManagementProjection.ToOutcomeDto).ToList();
	}
}
