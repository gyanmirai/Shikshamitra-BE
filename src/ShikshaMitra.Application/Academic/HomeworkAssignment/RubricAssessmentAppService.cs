using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.HomeworkAssignment;

[Authorize]
public class RubricAssessmentAppService(
	IRepository<AssessmentRubric, Guid> rubricRepository,
	IRepository<AssessmentRubricCriterion, Guid> criterionRepository,
	IRepository<RubricEvaluation, Guid> evaluationRepository,
	IRepository<RubricCriterionScore, Guid> scoreRepository,
	IRepository<HomeworkAssignmentEntity, Guid> homeworkRepository,
	IRepository<AssignmentEntity, Guid> assignmentRepository,
	IRepository<StudentSubmissionEntity, Guid> submissionRepository
) : ApplicationService, IRubricAssessmentAppService
{
	public async Task<AssessmentRubricDto> CreateAsync(CreateUpdateRubricDto input)
	{
		await EnsureTaskLinkIsValidAsync(input);
		EnsureCriteriaAreValid(input.Criteria);

		var rubric = new AssessmentRubric(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			input.StandardId,
			input.SubjectId,
			input.HomeworkId,
			input.AssignmentId,
			input.Title,
			input.Description);

		rubric.ReplaceCriteria(BuildCriteria(rubric.Id, input.Criteria));
		await rubricRepository.InsertAsync(rubric, autoSave: true);
		return MapRubric(rubric);
	}

	public async Task<List<AssessmentRubricDto>> GetListAsync(GetRubricsInput input)
	{
		var rubrics = await rubricRepository.GetListAsync(r =>
			(!input.StandardId.HasValue || r.StandardId == input.StandardId.Value) &&
			(!input.SubjectId.HasValue || r.SubjectId == input.SubjectId.Value) &&
			(!input.HomeworkId.HasValue || r.HomeworkId == input.HomeworkId.Value) &&
			(!input.AssignmentId.HasValue || r.AssignmentId == input.AssignmentId.Value) &&
			(!input.Status.HasValue || r.Status == input.Status.Value));

		if (!string.IsNullOrWhiteSpace(input.Search))
		{
			var search = input.Search.Trim();
			rubrics = rubrics
				.Where(r => r.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
					(r.Description?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false))
				.ToList();
		}

		await LoadCriteriaAsync(rubrics);
		return rubrics.OrderBy(r => r.Title).Select(MapRubric).ToList();
	}

	public async Task<AssessmentRubricDto> GetAsync(Guid id)
	{
		var rubric = await rubricRepository.GetAsync(id);
		await LoadCriteriaAsync([rubric]);
		return MapRubric(rubric);
	}

	public async Task<AssessmentRubricDto> UpdateAsync(Guid id, CreateUpdateRubricDto input)
	{
		await EnsureTaskLinkIsValidAsync(input);
		EnsureCriteriaAreValid(input.Criteria);

		var rubric = await rubricRepository.GetAsync(id);
		rubric.UpdateDetails(
			input.StandardId,
			input.SubjectId,
			input.HomeworkId,
			input.AssignmentId,
			input.Title,
			input.Description);

		var oldCriteria = await criterionRepository.GetListAsync(c => c.RubricId == id);
		foreach (var criterion in oldCriteria)
		{
			await criterionRepository.DeleteAsync(criterion, autoSave: false);
		}

		rubric.ReplaceCriteria(BuildCriteria(rubric.Id, input.Criteria));
		await rubricRepository.UpdateAsync(rubric, autoSave: true);
		return MapRubric(rubric);
	}

	public async Task PublishAsync(Guid id)
	{
		var rubric = await rubricRepository.GetAsync(id);
		await LoadCriteriaAsync([rubric]);
		rubric.Publish();
		await rubricRepository.UpdateAsync(rubric, autoSave: true);
	}

	public async Task ArchiveAsync(Guid id)
	{
		var rubric = await rubricRepository.GetAsync(id);
		rubric.Archive();
		await rubricRepository.UpdateAsync(rubric, autoSave: true);
	}

	public async Task<RubricEvaluationDto> UpsertEvaluationAsync(Guid rubricId, UpsertRubricEvaluationDto input)
	{
		var rubric = await rubricRepository.GetAsync(rubricId);
		await LoadCriteriaAsync([rubric]);
		if (rubric.Status != RubricStatus.Published)
		{
			throw new BusinessException("AssessmentRubric:RubricMustBePublished");
		}

		var submission = await submissionRepository.GetAsync(input.StudentSubmissionId);
		EnsureSubmissionMatchesRubric(rubric, submission, input.StudentId);
		EnsureScoresAreValid(rubric, input.Scores);

		var existing = await evaluationRepository.FirstOrDefaultAsync(e =>
			e.RubricId == rubricId && e.StudentSubmissionId == input.StudentSubmissionId);

		var evaluation = existing ?? new RubricEvaluation(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			rubricId,
			input.StudentSubmissionId,
			input.StudentId,
			CurrentUser.Id ?? Guid.Empty,
			input.Remarks);

		if (existing != null)
		{
			await LoadScoresAsync([evaluation]);
			evaluation.UpdateRemarks(input.Remarks);
			foreach (var score in evaluation.Scores.ToList())
			{
				await scoreRepository.DeleteAsync(score, autoSave: false);
			}
		}

		evaluation.ReplaceScores(input.Scores.Select(s => new RubricCriterionScore(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			evaluation.Id,
			s.CriterionId,
			s.Marks,
			s.Comments)));

		if (existing == null)
		{
			await evaluationRepository.InsertAsync(evaluation, autoSave: true);
		}
		else
		{
			await evaluationRepository.UpdateAsync(evaluation, autoSave: true);
		}

		return MapEvaluation(evaluation);
	}

	public async Task<List<RubricEvaluationDto>> GetEvaluationsAsync(Guid rubricId)
	{
		var evaluations = await evaluationRepository.GetListAsync(e => e.RubricId == rubricId);
		await LoadScoresAsync(evaluations);
		return evaluations.Select(MapEvaluation).ToList();
	}

	public async Task SubmitEvaluationAsync(Guid evaluationId)
	{
		var evaluation = await evaluationRepository.GetAsync(evaluationId);
		await LoadScoresAsync([evaluation]);
		if (evaluation.Scores.Count == 0)
		{
			throw new BusinessException("AssessmentRubric:ScoresRequiredBeforeSubmit");
		}

		evaluation.Submit();
		await evaluationRepository.UpdateAsync(evaluation, autoSave: true);
	}

	public async Task ApproveEvaluationAsync(Guid evaluationId)
	{
		var evaluation = await evaluationRepository.GetAsync(evaluationId);
		await LoadScoresAsync([evaluation]);
		var rubric = await rubricRepository.GetAsync(evaluation.RubricId);
		var submission = await submissionRepository.GetAsync(evaluation.StudentSubmissionId);

		evaluation.Approve();
		submission.UpdateFeedback(
			evaluation.TotalMarks,
			rubric.TotalMarks,
			null,
			evaluation.Remarks,
			SubmissionStatus.Reviewed,
			CurrentUser.Id ?? Guid.Empty);

		await evaluationRepository.UpdateAsync(evaluation, autoSave: false);
		await submissionRepository.UpdateAsync(submission, autoSave: true);
	}

	private async Task EnsureTaskLinkIsValidAsync(CreateUpdateRubricDto input)
	{
		if (input.HomeworkId.HasValue && input.AssignmentId.HasValue)
		{
			throw new BusinessException("AssessmentRubric:OnlyOneTaskLinkAllowed");
		}

		if (input.HomeworkId.HasValue)
		{
			var homework = await homeworkRepository.GetAsync(input.HomeworkId.Value);
			if (homework.StandardId != input.StandardId || homework.SubjectId != input.SubjectId)
			{
				throw new BusinessException("AssessmentRubric:TaskDoesNotMatchSubject");
			}
		}

		if (input.AssignmentId.HasValue)
		{
			var assignment = await assignmentRepository.GetAsync(input.AssignmentId.Value);
			if (assignment.StandardId != input.StandardId || assignment.SubjectId != input.SubjectId)
			{
				throw new BusinessException("AssessmentRubric:TaskDoesNotMatchSubject");
			}
		}
	}

	private static void EnsureCriteriaAreValid(IReadOnlyCollection<RubricCriterionInputDto> criteria)
	{
		if (criteria.Count == 0)
		{
			throw new BusinessException("AssessmentRubric:CriteriaRequired");
		}

		if (criteria.Any(c => string.IsNullOrWhiteSpace(c.Title) || c.MaxMarks <= 0))
		{
			throw new BusinessException("AssessmentRubric:InvalidCriteria");
		}
	}

	private static void EnsureScoresAreValid(AssessmentRubric rubric, IReadOnlyCollection<RubricEvaluationScoreInputDto> scores)
	{
		var criteria = rubric.Criteria.ToDictionary(c => c.Id);
		if (scores.Count != criteria.Count)
		{
			throw new BusinessException("AssessmentRubric:AllCriteriaMustBeScored");
		}

		var duplicateCriterion = scores
			.GroupBy(s => s.CriterionId)
			.Any(g => g.Count() > 1);
		if (duplicateCriterion)
		{
			throw new BusinessException("AssessmentRubric:DuplicateCriterionScore");
		}

		foreach (var score in scores)
		{
			if (!criteria.TryGetValue(score.CriterionId, out var criterion))
			{
				throw new BusinessException("AssessmentRubric:InvalidCriterion");
			}

			if (score.Marks < 0 || score.Marks > criterion.MaxMarks)
			{
				throw new BusinessException("AssessmentRubric:ScoreOutOfRange");
			}
		}
	}

	private static void EnsureSubmissionMatchesRubric(
		AssessmentRubric rubric,
		StudentSubmissionEntity submission,
		Guid studentId)
	{
		if (submission.StudentId != studentId)
		{
			throw new BusinessException("AssessmentRubric:SubmissionStudentMismatch");
		}

		if (rubric.HomeworkId.HasValue && submission.HomeworkAssignmentId != rubric.HomeworkId.Value)
		{
			throw new BusinessException("AssessmentRubric:SubmissionTaskMismatch");
		}

		if (rubric.AssignmentId.HasValue && submission.AssignmentId != rubric.AssignmentId.Value)
		{
			throw new BusinessException("AssessmentRubric:SubmissionTaskMismatch");
		}
	}

	private List<AssessmentRubricCriterion> BuildCriteria(Guid rubricId, IEnumerable<RubricCriterionInputDto> criteria)
	{
		return criteria
			.OrderBy(c => c.DisplayOrder)
			.Select((c, index) => new AssessmentRubricCriterion(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				rubricId,
				c.Title,
				c.Description,
				c.MaxMarks,
				c.DisplayOrder > 0 ? c.DisplayOrder : index + 1))
			.ToList();
	}

	private async Task LoadCriteriaAsync(IEnumerable<AssessmentRubric> rubrics)
	{
		foreach (var rubric in rubrics)
		{
			rubric.SetCriteriaForRead(await criterionRepository.GetListAsync(c => c.RubricId == rubric.Id));
		}
	}

	private async Task LoadScoresAsync(IEnumerable<RubricEvaluation> evaluations)
	{
		foreach (var evaluation in evaluations)
		{
			evaluation.SetScoresForRead(await scoreRepository.GetListAsync(s => s.EvaluationId == evaluation.Id));
		}
	}

	private static AssessmentRubricDto MapRubric(AssessmentRubric rubric)
	{
		return new AssessmentRubricDto
		{
			Id = rubric.Id,
			StandardId = rubric.StandardId,
			SubjectId = rubric.SubjectId,
			HomeworkId = rubric.HomeworkId,
			AssignmentId = rubric.AssignmentId,
			Title = rubric.Title,
			Description = rubric.Description,
			TotalMarks = rubric.TotalMarks,
			Version = rubric.Version,
			Status = rubric.Status,
			Criteria = rubric.Criteria
				.OrderBy(c => c.DisplayOrder)
				.Select(c => new AssessmentRubricCriterionDto
				{
					Id = c.Id,
					RubricId = c.RubricId,
					Title = c.Title,
					Description = c.Description,
					MaxMarks = c.MaxMarks,
					DisplayOrder = c.DisplayOrder
				})
				.ToList()
		};
	}

	private static RubricEvaluationDto MapEvaluation(RubricEvaluation evaluation)
	{
		return new RubricEvaluationDto
		{
			Id = evaluation.Id,
			RubricId = evaluation.RubricId,
			StudentSubmissionId = evaluation.StudentSubmissionId,
			StudentId = evaluation.StudentId,
			EvaluatorId = evaluation.EvaluatorId,
			TotalMarks = evaluation.TotalMarks,
			Remarks = evaluation.Remarks,
			Status = evaluation.Status,
			Scores = evaluation.Scores.Select(s => new RubricCriterionScoreDto
			{
				Id = s.Id,
				EvaluationId = s.EvaluationId,
				CriterionId = s.CriterionId,
				Marks = s.Marks,
				Comments = s.Comments
			}).ToList()
		};
	}
}
