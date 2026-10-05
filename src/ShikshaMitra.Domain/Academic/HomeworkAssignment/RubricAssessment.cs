using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public class AssessmentRubric : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid? StandardId { get; private set; }
	public Guid? SubjectId { get; private set; }
	public Guid? AcademicMediumId { get; private set; }
	public Guid? HomeworkId { get; private set; }
	public Guid? AssignmentId { get; private set; }
	public Guid? HomeworkTypeId { get; private set; }
	public Guid? AssignmentTypeId { get; private set; }
	public Guid? SourceTemplateId { get; private set; }
	public bool IsTemplate { get; private set; }
	public WorkType? AppliesToWorkType { get; private set; }
	public RubricScoringModel ScoringModel { get; private set; }
	public RubricVisibility Visibility { get; private set; }
	public string Title { get; private set; } = string.Empty;
	public string? Description { get; private set; }
	public string? EvaluationInstructions { get; private set; }
	public string? StudentInstructions { get; private set; }
	public string? TagsJson { get; private set; }
	public string? PerformanceLevelsJson { get; private set; }
	public decimal TotalMarks { get; private set; }
	public int Version { get; private set; }
	public RubricStatus Status { get; private set; }
	public ICollection<AssessmentRubricCriterion> Criteria { get; private set; } = new List<AssessmentRubricCriterion>();

	private AssessmentRubric() { }

	public AssessmentRubric(Guid id, Guid? tenantId, Guid? standardId, Guid? subjectId, Guid? homeworkId, Guid? assignmentId, string title, string? description)
		: base(id)
	{
		TenantId = tenantId;
		UpdateDetails(standardId, subjectId, homeworkId, assignmentId, title, description);
		Version = 1;
		Status = RubricStatus.Draft;
	}

	public AssessmentRubric(
		Guid id,
		Guid? tenantId,
		Guid? standardId,
		Guid? subjectId,
		Guid? academicMediumId,
		Guid? homeworkId,
		Guid? assignmentId,
		Guid? homeworkTypeId,
		Guid? assignmentTypeId,
		Guid? sourceTemplateId,
		bool isTemplate,
		WorkType? appliesToWorkType,
		RubricScoringModel scoringModel,
		RubricVisibility visibility,
		string title,
		string? description,
		string? evaluationInstructions,
		string? studentInstructions,
		string? tagsJson,
		string? performanceLevelsJson)
		: base(id)
	{
		TenantId = tenantId;
		UpdateDetails(
			standardId,
			subjectId,
			academicMediumId,
			homeworkId,
			assignmentId,
			homeworkTypeId,
			assignmentTypeId,
			sourceTemplateId,
			isTemplate,
			appliesToWorkType,
			scoringModel,
			visibility,
			title,
			description,
			evaluationInstructions,
			studentInstructions,
			tagsJson,
			performanceLevelsJson);
		Version = 1;
		Status = RubricStatus.Draft;
	}

	public void UpdateDetails(Guid? standardId, Guid? subjectId, Guid? homeworkId, Guid? assignmentId, string title, string? description)
	{
		UpdateDetails(
			standardId,
			subjectId,
			AcademicMediumId,
			homeworkId,
			assignmentId,
			HomeworkTypeId,
			AssignmentTypeId,
			SourceTemplateId,
			IsTemplate,
			AppliesToWorkType,
			ScoringModel,
			Visibility,
			title,
			description,
			EvaluationInstructions,
			StudentInstructions,
			TagsJson,
			PerformanceLevelsJson);
	}

	public void UpdateDetails(
		Guid? standardId,
		Guid? subjectId,
		Guid? academicMediumId,
		Guid? homeworkId,
		Guid? assignmentId,
		Guid? homeworkTypeId,
		Guid? assignmentTypeId,
		Guid? sourceTemplateId,
		bool isTemplate,
		WorkType? appliesToWorkType,
		RubricScoringModel scoringModel,
		RubricVisibility visibility,
		string title,
		string? description,
		string? evaluationInstructions,
		string? studentInstructions,
		string? tagsJson,
		string? performanceLevelsJson)
	{
		if (Status == RubricStatus.Published)
		{
			throw new BusinessException("AssessmentRubric:PublishedRubricIsLocked");
		}

		if (homeworkId.HasValue && assignmentId.HasValue)
		{
			throw new BusinessException("AssessmentRubric:OnlyOneTaskLinkAllowed");
		}

		if (isTemplate && (homeworkId.HasValue || assignmentId.HasValue))
		{
			throw new BusinessException("AssessmentRubric:TemplateCannotBeTaskLinked");
		}

		if (isTemplate && !appliesToWorkType.HasValue)
		{
			throw new BusinessException("AssessmentRubric:TemplateWorkTypeRequired");
		}

		if (homeworkTypeId.HasValue && assignmentTypeId.HasValue)
		{
			throw new BusinessException("AssessmentRubric:OnlyOneTypeLinkAllowed");
		}

		if (homeworkTypeId.HasValue && appliesToWorkType is not (WorkType.Homework or WorkType.Both))
		{
			throw new BusinessException("AssessmentRubric:HomeworkTypeWorkTypeMismatch");
		}

		if (assignmentTypeId.HasValue && appliesToWorkType is not (WorkType.Assignment or WorkType.Both))
		{
			throw new BusinessException("AssessmentRubric:AssignmentTypeWorkTypeMismatch");
		}

		if (!isTemplate && (!standardId.HasValue || standardId.Value == Guid.Empty ||
			!subjectId.HasValue || subjectId.Value == Guid.Empty))
		{
			throw new BusinessException("AssessmentRubric:RequiredFieldMissing");
		}
		StandardId = standardId.HasValue && standardId.Value != Guid.Empty ? standardId.Value : null;
		SubjectId = subjectId;
		AcademicMediumId = academicMediumId;
		HomeworkId = homeworkId;
		AssignmentId = assignmentId;
		HomeworkTypeId = homeworkTypeId;
		AssignmentTypeId = assignmentTypeId;
		SourceTemplateId = sourceTemplateId;
		IsTemplate = isTemplate;
		AppliesToWorkType = appliesToWorkType;
		ScoringModel = scoringModel;
		Visibility = visibility;
		Title = Normalize(title, 256);
		Description = NormalizeNullable(description, 1024);
		EvaluationInstructions = NormalizeNullable(evaluationInstructions, 2048);
		StudentInstructions = NormalizeNullable(studentInstructions, 2048);
		TagsJson = NormalizeNullable(tagsJson, 4096);
		PerformanceLevelsJson = NormalizeNullable(performanceLevelsJson, 8192);
	}

	public void ReplaceCriteria(IEnumerable<AssessmentRubricCriterion> criteria)
	{
		if (Status == RubricStatus.Published)
		{
			throw new BusinessException("AssessmentRubric:PublishedRubricIsLocked");
		}

		Criteria = criteria.ToList();
		TotalMarks = Criteria.Sum(GetCriterionMaximum);
		if (ScoringModel == RubricScoringModel.WeightedPercentage && TotalMarks != 100m)
		{
			throw new BusinessException("AssessmentRubric:WeightMustEqual100", "Criterion weights must total exactly 100%.");
		}
		if (TotalMarks <= 0)
		{
			throw new BusinessException("AssessmentRubric:CriteriaTotalRequired");
		}
	}

	public void SetCriteriaForRead(IEnumerable<AssessmentRubricCriterion> criteria)
	{
		Criteria = criteria.ToList();
		TotalMarks = Criteria.Sum(GetCriterionMaximum);
	}

	/// <summary>Resolves the scoring limit from shared levels, points or weight.</summary>
	public decimal GetCriterionMaximum(AssessmentRubricCriterion criterion)
	{
		if (ScoringModel == RubricScoringModel.PerformanceLevels)
		{
			using var levels = JsonDocument.Parse(PerformanceLevelsJson ?? "[]");
			return levels.RootElement.EnumerateArray()
				.Select(level => (level.TryGetProperty("Score", out var score) ? score : level.GetProperty("score")).GetDecimal())
				.DefaultIfEmpty(0).Max();
		}

		return ScoringModel == RubricScoringModel.WeightedPercentage
			? criterion.Weight ?? 0
			: criterion.Points ?? (ScoringModel == RubricScoringModel.Checklist ? 1 : 0);
	}

	public void Publish()
	{
		if (Criteria.Count == 0 || TotalMarks <= 0)
		{
			throw new BusinessException("AssessmentRubric:CriteriaRequiredBeforePublish");
		}

		Status = RubricStatus.Published;
	}

	public void Archive()
	{
		Status = RubricStatus.Archived;
	}

	private static string Normalize(string value, int maxLength)
	{
		if (string.IsNullOrWhiteSpace(value)) throw new BusinessException("AssessmentRubric:RequiredFieldMissing");
		var normalized = value.Trim();
		return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
	}

	private static string? NormalizeNullable(string? value, int maxLength)
	{
		if (string.IsNullOrWhiteSpace(value)) return null;
		var normalized = value.Trim();
		return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
	}
}

public class AssessmentRubricCriterion : FullAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid RubricId { get; private set; }
	public string Title { get; private set; } = string.Empty;
	public string? Description { get; private set; }
	public string? StudentDescription { get; private set; }
	public string? TeacherGuidance { get; private set; }
	public bool IsRequired { get; private set; }
	public decimal? Points { get; private set; }
	public decimal? Weight { get; private set; }
	public int DisplayOrder { get; private set; }

	private AssessmentRubricCriterion() { }

	public AssessmentRubricCriterion(
		Guid id,
		Guid? tenantId,
		Guid rubricId,
		string title,
		string? description,
		string? studentDescription,
		string? teacherGuidance,
		bool isRequired,
		decimal? points,
		int displayOrder,
		decimal? weight = null)
		: base(id)
	{
		TenantId = tenantId;
		RubricId = rubricId;
		Title = title.Trim();
		Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
		StudentDescription = string.IsNullOrWhiteSpace(studentDescription) ? null : studentDescription.Trim();
		TeacherGuidance = string.IsNullOrWhiteSpace(teacherGuidance) ? null : teacherGuidance.Trim();
		IsRequired = isRequired;
		Points = points;
		Weight = weight;
		DisplayOrder = displayOrder;
		if (Points is <= 0 || Weight is <= 0) throw new BusinessException("AssessmentRubric:CriterionMarksRequired");
	}
}

public class RubricEvaluation : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	private const string ApprovedEvaluationIsLockedCode = "AssessmentRubric:ApprovedEvaluationIsLocked";
	private const string ApprovedEvaluationIsLockedMessage =
		"This evaluation has already been approved and is locked. It cannot be modified.";

	public Guid? TenantId { get; private set; }
	public Guid RubricId { get; private set; }
	public Guid StudentSubmissionId { get; private set; }
	public Guid StudentId { get; private set; }
	public Guid EvaluatorId { get; private set; }
	public decimal TotalMarks { get; private set; }
	public string? Remarks { get; private set; }
	public RubricEvaluationStatus Status { get; private set; }
	public ICollection<RubricCriterionScore> Scores { get; private set; } = new List<RubricCriterionScore>();

	private RubricEvaluation() { }

	public RubricEvaluation(Guid id, Guid? tenantId, Guid rubricId, Guid studentSubmissionId, Guid studentId, Guid evaluatorId, string? remarks)
		: base(id)
	{
		TenantId = tenantId;
		RubricId = rubricId;
		StudentSubmissionId = studentSubmissionId;
		StudentId = studentId;
		EvaluatorId = evaluatorId;
		Remarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim();
		Status = RubricEvaluationStatus.Draft;
	}

	public void ReplaceScores(IEnumerable<RubricCriterionScore> scores)
	{
		if (Status == RubricEvaluationStatus.Approved)
		{
			throw new BusinessException(ApprovedEvaluationIsLockedCode, ApprovedEvaluationIsLockedMessage);
		}

		Scores = scores.ToList();
		TotalMarks = Scores.Sum(s => s.Marks);
	}

	public void SetScoresForRead(IEnumerable<RubricCriterionScore> scores)
	{
		Scores = scores.ToList();
	}

	public void UpdateRemarks(string? remarks)
	{
		if (Status == RubricEvaluationStatus.Approved)
		{
			throw new BusinessException(ApprovedEvaluationIsLockedCode, ApprovedEvaluationIsLockedMessage);
		}

		Remarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim();
	}

	public void Submit()
	{
		Status = RubricEvaluationStatus.Submitted;
	}

	public void Approve()
	{
		if (Status != RubricEvaluationStatus.Submitted)
		{
			throw new BusinessException("AssessmentRubric:SubmitBeforeApprove");
		}

		Status = RubricEvaluationStatus.Approved;
	}
}

public class RubricCriterionScore : FullAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid EvaluationId { get; private set; }
	public Guid CriterionId { get; private set; }
	public decimal Marks { get; private set; }
	public string? Comments { get; private set; }

	private RubricCriterionScore() { }

	public RubricCriterionScore(Guid id, Guid? tenantId, Guid evaluationId, Guid criterionId, decimal marks, string? comments)
		: base(id)
	{
		TenantId = tenantId;
		EvaluationId = evaluationId;
		CriterionId = criterionId;
		if (marks < 0) throw new BusinessException("AssessmentRubric:NegativeScoreNotAllowed");
		Marks = Math.Round(marks, 2, MidpointRounding.AwayFromZero);
		Comments = string.IsNullOrWhiteSpace(comments) ? null : comments.Trim();
		if (Marks < 0) throw new BusinessException("AssessmentRubric:NegativeScoreNotAllowed");
	}
}
