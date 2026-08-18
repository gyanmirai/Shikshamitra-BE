using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
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
		IStudentSubmissionRepository submissionRepository,
		IUserScopeService userScopeService

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
			input.AcademicMediumId,
			input.HomeworkId,
			input.AssignmentId,
			input.HomeworkTypeId,
			input.AssignmentTypeId,
			null,
			input.IsTemplate,
			input.AppliesToWorkType,
			input.ScoringModel,
			input.Visibility,
			input.Title,
			input.Description,
			input.EvaluationInstructions,
			input.StudentInstructions,
			SerializeList(input.Tags),
			SerializeList(input.PerformanceLevels));

		rubric.ReplaceCriteria(BuildCriteria(rubric.Id, input.Criteria));
		await rubricRepository.InsertAsync(rubric, autoSave: true);
		return MapRubric(rubric);
	}

	public async Task<List<AssessmentRubricDto>> GetListAsync(GetRubricsInput input)
	{
		var rubrics = await rubricRepository.GetListAsync(r =>
			(!input.StandardId.HasValue || r.StandardId == input.StandardId.Value) &&
			(!input.SubjectId.HasValue || r.SubjectId == input.SubjectId.Value) &&
			(!input.AcademicMediumId.HasValue || !r.AcademicMediumId.HasValue || r.AcademicMediumId == input.AcademicMediumId.Value) &&
			(!input.HomeworkId.HasValue || r.HomeworkId == input.HomeworkId.Value) &&
			(!input.AssignmentId.HasValue || r.AssignmentId == input.AssignmentId.Value) &&
			(!input.HomeworkTypeId.HasValue || r.HomeworkTypeId == input.HomeworkTypeId.Value) &&
			(!input.AssignmentTypeId.HasValue || r.AssignmentTypeId == input.AssignmentTypeId.Value) &&
			(!input.IsTemplate.HasValue || r.IsTemplate == input.IsTemplate.Value) &&
			(!input.AppliesToWorkType.HasValue || !r.AppliesToWorkType.HasValue || r.AppliesToWorkType == input.AppliesToWorkType.Value) &&
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
			input.AcademicMediumId,
			input.HomeworkId,
			input.AssignmentId,
			input.HomeworkTypeId,
			input.AssignmentTypeId,
			rubric.SourceTemplateId,
			input.IsTemplate,
			input.AppliesToWorkType,
			input.ScoringModel,
			input.Visibility,
			input.Title,
			input.Description,
			input.EvaluationInstructions,
			input.StudentInstructions,
			SerializeList(input.Tags),
			SerializeList(input.PerformanceLevels));

		var oldCriteria = await criterionRepository.GetListAsync(c => c.RubricId == id);
		foreach (var criterion in oldCriteria)
		{
			await criterionRepository.DeleteAsync(criterion, autoSave: false);
		}

		rubric.ReplaceCriteria(BuildCriteria(rubric.Id, input.Criteria));
		await rubricRepository.UpdateAsync(rubric, autoSave: true);
		return MapRubric(rubric);
	}

	public async Task<AssessmentRubricDto> CreateFromTemplateAsync(Guid templateId, CreateRubricFromTemplateDto input)
	{
		var template = await rubricRepository.GetAsync(templateId);
		if (!template.IsTemplate)
		{
			throw new BusinessException("AssessmentRubric:TemplateRequired");
		}

		if (template.Status != RubricStatus.Published)
		{
			throw new BusinessException("AssessmentRubric:TemplateMustBePublished");
		}

		await LoadCriteriaAsync([template]);
		var targetWorkType = input.HomeworkId.HasValue ? WorkType.Homework : WorkType.Assignment;
		if (template.AppliesToWorkType.HasValue && template.AppliesToWorkType.Value != targetWorkType)
		{
			throw new BusinessException("AssessmentRubric:TemplateWorkTypeMismatch");
		}

		var cloneInput = new CreateUpdateRubricDto
		{
			StandardId = input.StandardId,
			SubjectId = input.SubjectId,
			AcademicMediumId = template.AcademicMediumId,
			HomeworkId = input.HomeworkId,
			AssignmentId = input.AssignmentId,
			IsTemplate = false,
			AppliesToWorkType = targetWorkType,
			ScoringModel = template.ScoringModel,
			Visibility = template.Visibility,
			Title = string.IsNullOrWhiteSpace(input.Title) ? template.Title : input.Title!,
			Description = string.IsNullOrWhiteSpace(input.Description) ? template.Description : input.Description,
			EvaluationInstructions = template.EvaluationInstructions,
			StudentInstructions = template.StudentInstructions,
			Tags = DeserializeList<string>(template.TagsJson),
			PerformanceLevels = DeserializeList<RubricPerformanceLevelDto>(template.PerformanceLevelsJson),
			Criteria = template.Criteria
				.OrderBy(c => c.DisplayOrder)
				.Select(c => new RubricCriterionInputDto
				{
					Title = c.Title,
					Description = c.Description,
					MaxMarks = c.MaxMarks,
					DisplayOrder = c.DisplayOrder
				})
				.ToList()
		};

		await EnsureTaskLinkIsValidAsync(cloneInput);
		var rubric = new AssessmentRubric(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			cloneInput.StandardId,
			cloneInput.SubjectId,
			cloneInput.AcademicMediumId,
			cloneInput.HomeworkId,
			cloneInput.AssignmentId,
			null,
			null,
			template.Id,
			false,
			cloneInput.AppliesToWorkType,
			cloneInput.ScoringModel,
			cloneInput.Visibility,
			cloneInput.Title,
			cloneInput.Description,
			cloneInput.EvaluationInstructions,
			cloneInput.StudentInstructions,
			SerializeList(cloneInput.Tags),
			SerializeList(cloneInput.PerformanceLevels));

		rubric.ReplaceCriteria(BuildCriteria(rubric.Id, cloneInput.Criteria));
		rubric.Publish();
		await rubricRepository.InsertAsync(rubric, autoSave: true);
		await LoadCriteriaAsync([rubric]);
		return MapRubric(rubric);
	}

	public async Task PublishAsync(Guid id)
	{
		var rubric = await rubricRepository.GetAsync(id);
		await LoadCriteriaAsync([rubric]);
		EnsureRubricCanBePublished(rubric);
		rubric.Publish();
		await rubricRepository.UpdateAsync(rubric, autoSave: true);
	}

	public async Task ArchiveAsync(Guid id)
	{
		var rubric = await rubricRepository.GetAsync(id);
		rubric.Archive();
		await rubricRepository.UpdateAsync(rubric, autoSave: true);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_AssessmentAndFeedback.Add)]
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
		await EnsureCurrentUserCanAccessSubmissionAsync(submission, nameof(UpsertEvaluationAsync));
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

	[Authorize(ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_AssessmentAndFeedback.View)]
	public async Task<List<RubricEvaluationDto>> GetEvaluationsAsync(Guid rubricId)
	{
		var rubric = await rubricRepository.GetAsync(rubricId);
		await EnsureCurrentUserCanAccessRubricAsync(rubric, nameof(GetEvaluationsAsync));
		var evaluations = await evaluationRepository.GetListAsync(e => e.RubricId == rubricId);
		await LoadScoresAsync(evaluations);
		return evaluations.Select(MapEvaluation).ToList();
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_AssessmentAndFeedback.View)]
	public async Task<RubricEvaluationDto> GetStudentAssignmentEvaluationAsync(Guid studentId, Guid assignmentId)
	{
		var submission = await submissionRepository.GetSubmissionByStudentAndAssignmentAsync(studentId, assignmentId);
		if (submission == null)
		{
			throw new EntityNotFoundException(typeof(StudentSubmissionEntity), studentId);
		}

		await EnsureCurrentUserCanAccessSubmissionAsync(submission, nameof(GetStudentAssignmentEvaluationAsync));
		var evaluation = await evaluationRepository.FirstOrDefaultAsync(e =>
			e.StudentSubmissionId == submission.Id && e.StudentId == studentId);
		if (evaluation == null)
		{
			throw new EntityNotFoundException(typeof(RubricEvaluation), submission.Id);
		}

		await LoadScoresAsync([evaluation]);
		return MapEvaluation(evaluation);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_AssessmentAndFeedback.Add)]
	public async Task SubmitEvaluationAsync(Guid evaluationId)
	{
		var evaluation = await evaluationRepository.GetAsync(evaluationId);
		await EnsureCurrentUserCanAccessEvaluationAsync(evaluation, nameof(SubmitEvaluationAsync));
		await LoadScoresAsync([evaluation]);
		if (evaluation.Scores.Count == 0)
		{
			throw new BusinessException("AssessmentRubric:ScoresRequiredBeforeSubmit");
		}

		evaluation.Submit();
		await evaluationRepository.UpdateAsync(evaluation, autoSave: true);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_AssessmentAndFeedback.Add)]
	public async Task ApproveEvaluationAsync(Guid evaluationId)
	{
		var evaluation = await evaluationRepository.GetAsync(evaluationId);
		await EnsureCurrentUserCanAccessEvaluationAsync(evaluation, nameof(ApproveEvaluationAsync));
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

		if ((input.HomeworkId.HasValue || input.AssignmentId.HasValue) &&
			(!input.StandardId.HasValue || input.StandardId.Value == Guid.Empty ||
			!input.SubjectId.HasValue || input.SubjectId.Value == Guid.Empty))
		{
			throw new BusinessException("AssessmentRubric:RequiredFieldMissing");
		}

		if (input.HomeworkId.HasValue)
		{
			var homework = await homeworkRepository.GetAsync(input.HomeworkId.Value);
			if (homework.StandardId != input.StandardId!.Value || homework.SubjectId != input.SubjectId!.Value)
			{
				throw new BusinessException("AssessmentRubric:TaskDoesNotMatchSubject");
			}
		}

		if (input.AssignmentId.HasValue)
		{
			var assignment = await assignmentRepository.GetAsync(input.AssignmentId.Value);
			if (assignment.StandardId != input.StandardId!.Value || assignment.SubjectId != input.SubjectId!.Value)
			{
				throw new BusinessException("AssessmentRubric:TaskDoesNotMatchSubject");
			}

			var evaluation = AssignmentEvaluationResolver.Resolve(assignment, assignment.AssignmentType);
			if (!evaluation.IsRequired || evaluation.Method != EvaluationMethod.Rubric)
			{
				throw new BusinessException("AssessmentRubric:AssignmentRequiresRubricEvaluation")
					.WithData("AssignmentId", assignment.Id);
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

	private static void EnsureRubricCanBePublished(AssessmentRubric rubric)
	{
		if (rubric.ScoringModel == RubricScoringModel.WeightedPercentage && rubric.TotalMarks != 100)
		{
			throw new BusinessException("AssessmentRubric:WeightMustEqual100");
		}

		if (rubric.ScoringModel == RubricScoringModel.PerformanceLevels)
		{
			EnsurePerformanceLevelsAreValid(DeserializeList<RubricPerformanceLevelDto>(rubric.PerformanceLevelsJson));
		}
	}

	private static void EnsurePerformanceLevelsAreValid(IReadOnlyCollection<RubricPerformanceLevelDto> levels)
	{
		if (levels.Count < 2)
		{
			throw new BusinessException("AssessmentRubric:PerformanceLevelsRequired");
		}

		if (levels.Any(level => string.IsNullOrWhiteSpace(level.Label) || level.Score < 0))
		{
			throw new BusinessException("AssessmentRubric:InvalidPerformanceLevel");
		}

		if (levels.GroupBy(level => level.Label.Trim().ToUpperInvariant()).Any(group => group.Count() > 1))
		{
			throw new BusinessException("AssessmentRubric:DuplicatePerformanceLevel");
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

	private async Task EnsureCurrentUserCanAccessRubricAsync(AssessmentRubric rubric, string operation)
	{
		if (!rubric.AssignmentId.HasValue)
		{
			return;
		}

		var assignment = await assignmentRepository.GetAsync(rubric.AssignmentId.Value, includeDetails: true);
		await EnsureCurrentUserCanAccessAssignmentDivisionAsync(assignment, null, operation);
	}

	private async Task EnsureCurrentUserCanAccessEvaluationAsync(RubricEvaluation evaluation, string operation)
	{
		var submission = await submissionRepository.GetAsync(evaluation.StudentSubmissionId);
		await EnsureCurrentUserCanAccessSubmissionAsync(submission, operation);
	}

	private async Task EnsureCurrentUserCanAccessSubmissionAsync(StudentSubmissionEntity submission, string operation)
	{
		if (!submission.AssignmentId.HasValue)
		{
			return;
		}

		var assignment = await assignmentRepository.GetAsync(submission.AssignmentId.Value, includeDetails: true);
		await EnsureCurrentUserCanAccessAssignmentDivisionAsync(assignment, submission.DivisionId, operation);
	}

	private async Task EnsureCurrentUserCanAccessAssignmentDivisionAsync(
		AssignmentEntity assignment,
		Guid? divisionId,
		string operation)
	{
		if (!CurrentUser.Id.HasValue)
		{
			throw new BusinessException(HomeworkAssignmentErrorCodes.AccessDenied);
		}

		var scope = await userScopeService.GetUserScopeAsync(CurrentUser.Id.Value);
		var allowed = divisionId.HasValue
			? AssignmentScopePolicy.CanAccessDivision(scope, assignment, divisionId.Value)
			: AssignmentScopePolicy.CanAccessAggregate(scope, assignment);
		if (!allowed)
		{
			throw new BusinessException(HomeworkAssignmentErrorCodes.AccessDenied)
				.WithData("AssignmentId", assignment.Id)
				.WithData("Operation", operation);
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
			AcademicMediumId = rubric.AcademicMediumId,
			HomeworkId = rubric.HomeworkId,
			AssignmentId = rubric.AssignmentId,
			HomeworkTypeId = rubric.HomeworkTypeId,
			AssignmentTypeId = rubric.AssignmentTypeId,
			SourceTemplateId = rubric.SourceTemplateId,
			IsTemplate = rubric.IsTemplate,
			AppliesToWorkType = rubric.AppliesToWorkType,
			ScoringModel = rubric.ScoringModel,
			Visibility = rubric.Visibility,
			Title = rubric.Title,
			Description = rubric.Description,
			EvaluationInstructions = rubric.EvaluationInstructions,
			StudentInstructions = rubric.StudentInstructions,
			Tags = DeserializeList<string>(rubric.TagsJson),
			PerformanceLevels = DeserializeList<RubricPerformanceLevelDto>(rubric.PerformanceLevelsJson),
			TotalMarks = rubric.TotalMarks,
			Version = rubric.Version,
			Status = rubric.Status,
			CreationTime = rubric.CreationTime,
			LastModificationTime = rubric.LastModificationTime,
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

	private static string? SerializeList<T>(IReadOnlyCollection<T>? items)
	{
		return items == null || items.Count == 0 ? null : JsonSerializer.Serialize(items);
	}

	private static List<T> DeserializeList<T>(string? json)
	{
		if (string.IsNullOrWhiteSpace(json))
		{
			return [];
		}

		return JsonSerializer.Deserialize<List<T>>(json) ?? [];
	}
}
