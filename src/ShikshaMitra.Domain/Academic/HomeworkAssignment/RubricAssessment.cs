using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public class AssessmentRubric : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid StandardId { get; private set; }
	public Guid SubjectId { get; private set; }
	public Guid? HomeworkId { get; private set; }
	public Guid? AssignmentId { get; private set; }
	public string Title { get; private set; } = string.Empty;
	public string? Description { get; private set; }
	public decimal TotalMarks { get; private set; }
	public int Version { get; private set; }
	public RubricStatus Status { get; private set; }
	public ICollection<AssessmentRubricCriterion> Criteria { get; private set; } = new List<AssessmentRubricCriterion>();

	private AssessmentRubric() { }

	public AssessmentRubric(Guid id, Guid? tenantId, Guid standardId, Guid subjectId, Guid? homeworkId, Guid? assignmentId, string title, string? description)
		: base(id)
	{
		TenantId = tenantId;
		UpdateDetails(standardId, subjectId, homeworkId, assignmentId, title, description);
		Version = 1;
		Status = RubricStatus.Draft;
	}

	public void UpdateDetails(Guid standardId, Guid subjectId, Guid? homeworkId, Guid? assignmentId, string title, string? description)
	{
		if (Status == RubricStatus.Published)
		{
			throw new BusinessException("AssessmentRubric:PublishedRubricIsLocked");
		}

		if (homeworkId.HasValue && assignmentId.HasValue)
		{
			throw new BusinessException("AssessmentRubric:OnlyOneTaskLinkAllowed");
		}

		StandardId = standardId == Guid.Empty ? throw new BusinessException("AssessmentRubric:RequiredFieldMissing") : standardId;
		SubjectId = subjectId == Guid.Empty ? throw new BusinessException("AssessmentRubric:RequiredFieldMissing") : subjectId;
		HomeworkId = homeworkId;
		AssignmentId = assignmentId;
		Title = Normalize(title, 256);
		Description = NormalizeNullable(description, 1024);
	}

	public void ReplaceCriteria(IEnumerable<AssessmentRubricCriterion> criteria)
	{
		if (Status == RubricStatus.Published)
		{
			throw new BusinessException("AssessmentRubric:PublishedRubricIsLocked");
		}

		Criteria = criteria.ToList();
		TotalMarks = Criteria.Sum(c => c.MaxMarks);
		if (TotalMarks <= 0)
		{
			throw new BusinessException("AssessmentRubric:CriteriaTotalRequired");
		}
	}

	public void SetCriteriaForRead(IEnumerable<AssessmentRubricCriterion> criteria)
	{
		Criteria = criteria.ToList();
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
	public decimal MaxMarks { get; private set; }
	public int DisplayOrder { get; private set; }

	private AssessmentRubricCriterion() { }

	public AssessmentRubricCriterion(Guid id, Guid? tenantId, Guid rubricId, string title, string? description, decimal maxMarks, int displayOrder)
		: base(id)
	{
		TenantId = tenantId;
		RubricId = rubricId;
		Title = title.Trim();
		Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
		MaxMarks = maxMarks;
		DisplayOrder = displayOrder;
		if (MaxMarks <= 0) throw new BusinessException("AssessmentRubric:CriterionMarksRequired");
	}
}

public class RubricEvaluation : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
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
			throw new BusinessException("AssessmentRubric:ApprovedEvaluationIsLocked");
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
			throw new BusinessException("AssessmentRubric:ApprovedEvaluationIsLocked");
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
		Marks = marks;
		Comments = string.IsNullOrWhiteSpace(comments) ? null : comments.Trim();
		if (Marks < 0) throw new BusinessException("AssessmentRubric:NegativeScoreNotAllowed");
	}
}
