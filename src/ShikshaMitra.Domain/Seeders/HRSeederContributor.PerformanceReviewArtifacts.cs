using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.ProfessionalManagement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ShikshaMitra.Seeders;

public partial class HRSeederContributor
{
	private async Task EnsurePerformanceReviewSeedDetailsAsync(
		PerformanceReviewAssignment assignment,
		Guid templateId,
		Employee employee,
		int index)
	{
		var criteria = (await _performanceTemplateCriterionRepository.GetQueryableAsync())
			.Where(x => x.TemplateId == templateId)
			.OrderBy(x => x.DisplayOrder)
			.ToList();
		if (criteria.Count == 0)
		{
			return;
		}

		var score = 72m + (index % 5) * 5m;
		var finalRating = score >= 88m ? "Excellent" : score >= 80m ? "Very Good" : "Good";
		var submittedStages = (index % 4) switch
		{
			0 => new[] { PerformanceReviewStageType.SelfAppraisal },
			1 => new[] { PerformanceReviewStageType.SelfAppraisal, PerformanceReviewStageType.ReportingManager },
			2 => new[] { PerformanceReviewStageType.SelfAppraisal, PerformanceReviewStageType.ReportingManager, PerformanceReviewStageType.HodCoordinator },
			_ => Enum.GetValues<PerformanceReviewStageType>()
		};

		foreach (var stageType in Enum.GetValues<PerformanceReviewStageType>())
		{
			var stage = await EnsurePerformanceStageAsync(assignment, stageType);
			if (stage.Status == PerformanceReviewStageStatus.Pending || stage.Status == PerformanceReviewStageStatus.Draft)
			{
				var comment = BuildPerformanceStageComment(employee, stageType);
				if (submittedStages.Contains(stageType))
				{
					stage.Submit(comment, _clock.Now.AddDays(-Math.Max(1, 10 - index)));
				}
				else
				{
					stage.SaveDraft(comment);
				}

				await _performanceStageRepository.UpdateAsync(stage, autoSave: true);
			}

			await EnsurePerformanceResponsesAsync(assignment.Id, stage.Id, criteria, index);
		}

		assignment.MarkInProgress();
		if (index % 4 == 3)
		{
			assignment.FinalizeReview(score, finalRating, _clock.Now.AddDays(-index));
			await EnsurePerformanceOutcomeAsync(assignment.Id, score, finalRating, index);
		}
		await _performanceAssignmentRepository.UpdateAsync(assignment, autoSave: true);

		if (index % 3 == 0)
		{
			await EnsureClassroomObservationAsync(assignment.Id, employee.Id, index);
		}

		await EnsurePerformanceEvidenceAsync(assignment.Id, criteria.First().Id, index);
		await EnsurePerformanceFeedbackAsync(assignment.Id, employee, index);
		if (index % 2 == 0)
		{
			await EnsureDevelopmentPlanAsync(assignment.Id, employee.Id, index);
		}
		await EnsurePerformanceAuditAsync(assignment.Id, index);
	}

	private async Task<PerformanceReviewStage> EnsurePerformanceStageAsync(
		PerformanceReviewAssignment assignment,
		PerformanceReviewStageType stageType)
	{
		var stageQuery = await _performanceStageRepository.GetQueryableAsync();
		var stage = stageQuery.FirstOrDefault(x => x.ReviewAssignmentId == assignment.Id && x.StageType == stageType);
		if (stage != null)
		{
			return stage;
		}

		stage = new PerformanceReviewStage(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			assignment.Id,
			stageType,
			stageType switch
			{
				PerformanceReviewStageType.ReportingManager => assignment.ReportingManagerEmployeeId,
				PerformanceReviewStageType.HodCoordinator => assignment.HodEmployeeId,
				PerformanceReviewStageType.PrincipalManagement => assignment.PrincipalEmployeeId,
				_ => null
			});
		await _performanceStageRepository.InsertAsync(stage, autoSave: true);
		return stage;
	}

	private async Task EnsurePerformanceResponsesAsync(
		Guid assignmentId,
		Guid stageId,
		IReadOnlyCollection<PerformanceTemplateCriterion> criteria,
		int index)
	{
		var responseQuery = await _performanceResponseRepository.GetQueryableAsync();
		if (responseQuery.Any(x => x.StageId == stageId))
		{
			return;
		}

		var rating = 3 + (index % 3);
		foreach (var criterion in criteria)
		{
			await _performanceResponseRepository.InsertAsync(
				new PerformanceReviewResponse(
					GuidGenerator.Create(),
					CurrentTenant.Id,
					assignmentId,
					stageId,
					criterion.Id,
					Math.Clamp(rating, 1, 5),
					false,
					"Seeded review response for demo appraisal flow."),
				autoSave: true);
		}
	}
}
