using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace ShikshaMitra.HR.ProfessionalManagement;

[RemoteService(false)]
public class ProfessionalManagementReviewWorkflow(
	IRepository<PerformanceTemplate, Guid> templateRepository,
	IRepository<PerformanceReviewAssignment, Guid> assignmentRepository,
	IRepository<PerformanceReviewStage, Guid> stageRepository,
	IRepository<PerformanceReviewResponse, Guid> responseRepository,
	IRepository<PerformanceEvidence, Guid> evidenceRepository,
	ProfessionalManagementReviewReader reviewReader,
	ProfessionalManagementTemplateWorkflow templateWorkflow,
	PerformanceAuditEventManager auditEventManager,
	PerformanceReviewStageManager stageManager,
	IGuidGenerator guidGenerator)
	: ApplicationService, ITransientDependency
{
	public async Task<ProfessionalReviewDto> SaveDraftAsync(Guid id, ProfessionalReviewSaveRequest input)
	{
		return await SaveStageAsync(id, new PerformanceReviewStageRequest { StageType = PerformanceReviewStageType.SelfAppraisal, SummaryComment = input.Comments }, false);
	}

	public async Task<ProfessionalReviewDto> SubmitAsync(Guid id, ProfessionalReviewSaveRequest input)
	{
		return await SaveStageAsync(id, new PerformanceReviewStageRequest { StageType = PerformanceReviewStageType.SelfAppraisal, SummaryComment = input.Comments }, true);
	}

	public async Task<ProfessionalReviewDto> SaveStageAsync(Guid id, PerformanceReviewStageRequest input, bool submit)
	{
		var assignment = await assignmentRepository.GetAsync(id);
		assignment.EnsureEditable();
		var stage = await FindStageAsync(id, input.StageType);
		var isNewStage = stage == null;
		stage ??= stageManager.CreateStage(assignment, input.StageType);
		if (isNewStage) await stageRepository.InsertAsync(stage, autoSave: true);
		if (submit)
		{
			await ValidateStageSubmissionAsync(assignment, input);
		}

		stageManager.SaveStage(stage, input.SummaryComment, submit, Clock.Now);
		await stageRepository.UpdateAsync(stage, autoSave: true);
		await UpsertResponsesAsync(assignment, stage, input.Responses);
		assignment.MarkInProgress();
		await assignmentRepository.UpdateAsync(assignment, autoSave: true);
		await AuditAsync(id, submit ? "StageSubmitted" : "StageSaved", $"{input.StageType} {(submit ? "submitted" : "saved")}");
		return await reviewReader.GetReviewAsync(id);
	}

	private async Task ValidateStageSubmissionAsync(PerformanceReviewAssignment assignment, PerformanceReviewStageRequest input)
	{
		var template = await templateRepository.GetAsync(assignment.TemplateId);
		var criteria = await templateWorkflow.GetCriteriaAsync(assignment.TemplateId);
		var criterionIds = criteria.Select(x => x.Id).ToHashSet();
		var responseCriterionIds = input.Responses.Select(x => x.CriterionId).ToList();
		if (responseCriterionIds.Count != responseCriterionIds.Distinct().Count() ||
		    responseCriterionIds.Any(x => !criterionIds.Contains(x)))
		{
			throw new BusinessException(HRErrorCodes.PerformanceReviewCriterionInvalid);
		}

		foreach (var response in input.Responses.Where(x => !x.IsNotApplicable))
		{
			if (!response.Rating.HasValue || response.Rating.Value < 1 || response.Rating.Value > template.RatingScaleMax)
			{
				throw new BusinessException(HRErrorCodes.PerformanceReviewCriterionInvalid).WithData("CriterionId", response.CriterionId).WithData("RatingScaleMax", template.RatingScaleMax);
			}
		}

		if (!template.AllowNotApplicable && input.Responses.Any(x => x.IsNotApplicable))
		{
			throw new BusinessException(HRErrorCodes.PerformanceReviewCriterionInvalid);
		}

		var requiredCriterionIds = criteria.Where(x => x.EvidenceRequired).Select(x => x.Id).ToHashSet();
		if (requiredCriterionIds.Count == 0) return;
		var evidenceQuery = await evidenceRepository.GetQueryableAsync();
		var evidenceCriterionIds = (await AsyncExecuter.ToListAsync(evidenceQuery.Where(x => x.ReviewAssignmentId == assignment.Id && x.CriterionId.HasValue))).Select(x => x.CriterionId!.Value).ToHashSet();
		var submittedCriterionIds = input.Responses.Where(x => !x.IsNotApplicable).Select(x => x.CriterionId).ToHashSet();
		if (requiredCriterionIds.Any(x => submittedCriterionIds.Contains(x) && !evidenceCriterionIds.Contains(x)))
		{
			throw new BusinessException(HRErrorCodes.PerformanceEvidenceRequired);
		}
	}

	private async Task UpsertResponsesAsync(PerformanceReviewAssignment assignment, PerformanceReviewStage stage, List<PerformanceReviewResponseRequest> responses)
	{
		var query = await responseRepository.GetQueryableAsync();
		var existing = await AsyncExecuter.ToListAsync(query.Where(x => x.StageId == stage.Id));
		foreach (var input in responses)
		{
			var row = existing.FirstOrDefault(x => x.CriterionId == input.CriterionId);
			if (row == null)
			{
				row = new PerformanceReviewResponse(guidGenerator.Create(), CurrentTenant.Id, assignment.Id, stage.Id, input.CriterionId, input.Rating, input.IsNotApplicable, input.Comment);
				await responseRepository.InsertAsync(row, autoSave: true);
			}
			else
			{
				row.Update(input.Rating, input.IsNotApplicable, input.Comment);
				await responseRepository.UpdateAsync(row, autoSave: true);
			}
		}
	}

	private async Task<PerformanceReviewStage?> FindStageAsync(Guid reviewId, PerformanceReviewStageType stageType)
	{
		var query = await stageRepository.GetQueryableAsync();
		return await AsyncExecuter.FirstOrDefaultAsync(query.Where(x => x.ReviewAssignmentId == reviewId && x.StageType == stageType));
	}

	private async Task AuditAsync(Guid? reviewId, string eventType, string summary) =>
		await auditEventManager.RecordAsync(reviewId, eventType, summary);
}
