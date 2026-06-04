using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp;

namespace ShikshaMitra.HR.ProfessionalManagement;

[RemoteService(false)]
public class ProfessionalManagementOutcomeWorkflow(
	IRepository<PerformanceTemplate, Guid> templateRepository,
	IRepository<PerformanceReviewAssignment, Guid> assignmentRepository,
	IRepository<PerformanceReviewStage, Guid> stageRepository,
	IRepository<PerformanceReviewResponse, Guid> responseRepository,
	IRepository<PerformanceOutcome, Guid> outcomeRepository,
	IRepository<DevelopmentPlan, Guid> developmentPlanRepository,
	ProfessionalManagementReviewReader reviewReader,
	ProfessionalManagementTemplateWorkflow templateWorkflow,
	PerformanceAuditEventManager auditEventManager,
	PerformanceOutcomeManager outcomeManager,
	IGuidGenerator guidGenerator)
	: ApplicationService, ITransientDependency
{
	public async Task<ProfessionalReviewDto> ReopenReviewAsync(Guid id, ReopenReviewRequest input)
	{
		var assignment = await assignmentRepository.GetAsync(id);
		assignment.Reopen();
		await assignmentRepository.UpdateAsync(assignment, autoSave: true);
		var stageQuery = await stageRepository.GetQueryableAsync();
		var submittedStages = await AsyncExecuter.ToListAsync(stageQuery.Where(x => x.ReviewAssignmentId == id && x.Status == PerformanceReviewStageStatus.Submitted));
		foreach (var stage in submittedStages)
		{
			stage.Reopen(input.Reason);
			await stageRepository.UpdateAsync(stage, autoSave: true);
		}

		await AuditAsync(id, "ReviewReopened", input.Reason);
		return await reviewReader.GetReviewAsync(id);
	}

	public async Task<PerformanceOutcomeDto> SaveFinalOutcomeAsync(Guid id, PerformanceOutcomeRequest input)
	{
		var assignment = await assignmentRepository.GetAsync(id);
		assignment.EnsureEditable();
		var score = input.FinalScore ?? await CalculateScoreAsync(assignment);
		var existing = await FindOutcomeAsync(id);
		var result = outcomeManager.SaveFinalOutcome(
			assignment,
			existing,
			score,
			input.FinalRating,
			input.Strengths,
			input.ImprovementAreas,
			input.Recommendation,
			input.RecommendationNotes,
			input.IncrementRecommended,
			input.PromotionRecommended,
			Clock.Now);
		if (result.IsNew)
		{
			await outcomeRepository.InsertAsync(result.Outcome, autoSave: true);
		}
		else
		{
			await outcomeRepository.UpdateAsync(result.Outcome, autoSave: true);
		}

		await assignmentRepository.UpdateAsync(assignment, autoSave: true);
		await AuditAsync(id, "OutcomeSaved", $"Saved final outcome {input.FinalRating}");
		return ProfessionalManagementProjection.ToOutcomeDto(result.Outcome);
	}

	public async Task<DevelopmentPlanDto> AddDevelopmentPlanAsync(Guid id, DevelopmentPlanRequest input)
	{
		(await assignmentRepository.GetAsync(id)).EnsureEditable();
		var plan = new DevelopmentPlan(guidGenerator.Create(), CurrentTenant.Id, id, input.Goal, input.ActionPlan, input.OwnerEmployeeId, input.TargetDate);
		plan.Update(input.Goal, input.ActionPlan, input.OwnerEmployeeId, input.TargetDate, input.Status);
		await developmentPlanRepository.InsertAsync(plan, autoSave: true);
		await AuditAsync(id, "DevelopmentPlanAdded", input.Goal);
		return ProfessionalManagementProjection.ToDevelopmentPlanDto(plan);
	}

	public async Task<DevelopmentPlanDto> UpdateDevelopmentPlanAsync(Guid id, DevelopmentPlanRequest input)
	{
		var plan = await developmentPlanRepository.GetAsync(id);
		(await assignmentRepository.GetAsync(plan.ReviewAssignmentId)).EnsureEditable();
		plan.Update(input.Goal, input.ActionPlan, input.OwnerEmployeeId, input.TargetDate, input.Status);
		await developmentPlanRepository.UpdateAsync(plan, autoSave: true);
		await AuditAsync(plan.ReviewAssignmentId, "DevelopmentPlanUpdated", input.Goal);
		return ProfessionalManagementProjection.ToDevelopmentPlanDto(plan);
	}

	private async Task<decimal> CalculateScoreAsync(PerformanceReviewAssignment assignment)
	{
		var sections = await templateWorkflow.GetSectionsAsync(assignment.TemplateId);
		var criteria = await templateWorkflow.GetCriteriaAsync(assignment.TemplateId);
		var responseQuery = await responseRepository.GetQueryableAsync();
		var responses = await AsyncExecuter.ToListAsync(responseQuery.Where(x => x.ReviewAssignmentId == assignment.Id));
		var template = await templateRepository.GetAsync(assignment.TemplateId);
		return PerformanceScoreCalculator.CalculateFinalScore(sections, criteria, responses, template.RatingScaleMax);
	}

	private async Task<PerformanceOutcome?> FindOutcomeAsync(Guid reviewId)
	{
		var query = await outcomeRepository.GetQueryableAsync();
		return await AsyncExecuter.FirstOrDefaultAsync(query.Where(x => x.ReviewAssignmentId == reviewId));
	}

	private async Task AuditAsync(Guid? reviewId, string eventType, string summary) =>
		await auditEventManager.RecordAsync(reviewId, eventType, summary);
}
