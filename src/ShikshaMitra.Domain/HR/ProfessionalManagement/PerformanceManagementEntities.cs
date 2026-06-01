using System;
using ShikshaMitra.HR;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.ProfessionalManagement;

public class PerformanceTemplate : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public string Name { get; private set; } = string.Empty;
	public PerformanceStaffCategory StaffCategory { get; private set; }
	public string? RoleName { get; private set; }
	public int RatingScaleMax { get; private set; } = 5;
	public bool AllowNotApplicable { get; private set; } = true;
	public PerformanceTemplateStatus Status { get; private set; }
	public string? Description { get; private set; }

	protected PerformanceTemplate()
	{
	}

	public PerformanceTemplate(Guid id, Guid? tenantId, string name, PerformanceStaffCategory staffCategory, string? roleName, string? description = null) : base(id)
	{
		TenantId = tenantId;
		Update(name, staffCategory, roleName, 5, true, description);
		Status = PerformanceTemplateStatus.Draft;
	}

	public void Update(string name, PerformanceStaffCategory staffCategory, string? roleName, int ratingScaleMax, bool allowNotApplicable, string? description)
	{
		if (Status == PerformanceTemplateStatus.Published)
		{
			throw new BusinessException(HRErrorCodes.PerformanceTemplatePublished);
		}

		Name = name.Trim();
		StaffCategory = staffCategory;
		RoleName = string.IsNullOrWhiteSpace(roleName) ? null : roleName.Trim();
		RatingScaleMax = Math.Clamp(ratingScaleMax, 1, 10);
		AllowNotApplicable = allowNotApplicable;
		Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
	}

	public void Publish(decimal totalWeight, bool hasValidRubric = true)
	{
		if (Math.Round(totalWeight, 2) != 100m)
		{
			throw new BusinessException(HRErrorCodes.PerformanceTemplateWeightsInvalid);
		}

		if (!hasValidRubric)
		{
			throw new BusinessException(HRErrorCodes.PerformanceTemplateRubricInvalid);
		}

		Status = PerformanceTemplateStatus.Published;
	}

	public void Archive()
	{
		Status = PerformanceTemplateStatus.Archived;
	}
}

public class PerformanceTemplateSection : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid TemplateId { get; private set; }
	public string Name { get; private set; } = string.Empty;
	public decimal Weight { get; private set; }
	public int DisplayOrder { get; private set; }

	protected PerformanceTemplateSection()
	{
	}

	public PerformanceTemplateSection(Guid id, Guid? tenantId, Guid templateId, string name, decimal weight, int displayOrder) : base(id)
	{
		TenantId = tenantId;
		TemplateId = templateId;
		Update(name, weight, displayOrder);
	}

	public void Update(string name, decimal weight, int displayOrder)
	{
		Name = name.Trim();
		Weight = Math.Clamp(weight, 0m, 100m);
		DisplayOrder = displayOrder;
	}
}

public class PerformanceTemplateCriterion : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid TemplateId { get; private set; }
	public Guid SectionId { get; private set; }
	public string Name { get; private set; } = string.Empty;
	public string? Description { get; private set; }
	public decimal Weight { get; private set; }
	public bool EvidenceRequired { get; private set; }
	public int DisplayOrder { get; private set; }

	protected PerformanceTemplateCriterion()
	{
	}

	public PerformanceTemplateCriterion(Guid id, Guid? tenantId, Guid templateId, Guid sectionId, string name, string? description, decimal weight, bool evidenceRequired, int displayOrder) : base(id)
	{
		TenantId = tenantId;
		TemplateId = templateId;
		SectionId = sectionId;
		Update(name, description, weight, evidenceRequired, displayOrder);
	}

	public void Update(string name, string? description, decimal weight, bool evidenceRequired, int displayOrder)
	{
		Name = name.Trim();
		Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
		Weight = Math.Clamp(weight, 0m, 100m);
		EvidenceRequired = evidenceRequired;
		DisplayOrder = displayOrder;
	}
}

public class PerformanceReviewAssignment : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid CycleId { get; private set; }
	public Guid TemplateId { get; private set; }
	public Guid EmployeeId { get; private set; }
	public Guid? ReportingManagerEmployeeId { get; private set; }
	public Guid? HodEmployeeId { get; private set; }
	public Guid? PrincipalEmployeeId { get; private set; }
	public ProfessionalReviewStatus Status { get; private set; }
	public decimal? FinalScore { get; private set; }
	public string? FinalRating { get; private set; }
	public DateTime? FinalizedAt { get; private set; }

	protected PerformanceReviewAssignment()
	{
	}

	public PerformanceReviewAssignment(Guid id, Guid? tenantId, Guid cycleId, Guid templateId, Guid employeeId, Guid? reportingManagerEmployeeId = null, Guid? hodEmployeeId = null, Guid? principalEmployeeId = null) : base(id)
	{
		TenantId = tenantId;
		CycleId = cycleId;
		TemplateId = templateId;
		EmployeeId = employeeId;
		ReportingManagerEmployeeId = reportingManagerEmployeeId;
		HodEmployeeId = hodEmployeeId;
		PrincipalEmployeeId = principalEmployeeId;
		Status = ProfessionalReviewStatus.Draft;
	}

	public void MarkInProgress()
	{
		if (Status != ProfessionalReviewStatus.Finalized)
		{
			Status = ProfessionalReviewStatus.InProgress;
		}
	}

	public void FinalizeReview(decimal finalScore, string finalRating, DateTime finalizedAt)
	{
		FinalScore = Math.Round(finalScore, 2);
		FinalRating = string.IsNullOrWhiteSpace(finalRating) ? null : finalRating.Trim();
		FinalizedAt = finalizedAt;
		Status = ProfessionalReviewStatus.Finalized;
	}

	public void Reopen()
	{
		Status = ProfessionalReviewStatus.Reopened;
		FinalizedAt = null;
	}

	public void EnsureEditable()
	{
		if (Status == ProfessionalReviewStatus.Finalized)
		{
			throw new BusinessException(HRErrorCodes.PerformanceReviewFinalized);
		}
	}
}

public class PerformanceReviewStage : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid ReviewAssignmentId { get; private set; }
	public PerformanceReviewStageType StageType { get; private set; }
	public Guid? ReviewerEmployeeId { get; private set; }
	public PerformanceReviewStageStatus Status { get; private set; }
	public string? SummaryComment { get; private set; }
	public DateTime? SubmittedAt { get; private set; }

	protected PerformanceReviewStage()
	{
	}

	public PerformanceReviewStage(Guid id, Guid? tenantId, Guid reviewAssignmentId, PerformanceReviewStageType stageType, Guid? reviewerEmployeeId = null) : base(id)
	{
		TenantId = tenantId;
		ReviewAssignmentId = reviewAssignmentId;
		StageType = stageType;
		ReviewerEmployeeId = reviewerEmployeeId;
		Status = PerformanceReviewStageStatus.Pending;
	}

	public void SaveDraft(string? summaryComment)
	{
		EnsureNotSubmitted();
		SummaryComment = string.IsNullOrWhiteSpace(summaryComment) ? null : summaryComment.Trim();
		Status = PerformanceReviewStageStatus.Draft;
		SubmittedAt = null;
	}

	public void Submit(string? summaryComment, DateTime submittedAt)
	{
		EnsureNotSubmitted();
		SummaryComment = string.IsNullOrWhiteSpace(summaryComment) ? null : summaryComment.Trim();
		Status = PerformanceReviewStageStatus.Submitted;
		SubmittedAt = submittedAt;
	}

	public void Reopen(string reason)
	{
		if (string.IsNullOrWhiteSpace(reason))
		{
			throw new BusinessException(HRErrorCodes.PerformanceReviewStageLocked);
		}

		Status = PerformanceReviewStageStatus.Reopened;
		SubmittedAt = null;
	}

	private void EnsureNotSubmitted()
	{
		if (Status == PerformanceReviewStageStatus.Submitted)
		{
			throw new BusinessException(HRErrorCodes.PerformanceReviewStageLocked);
		}
	}
}

public class PerformanceReviewResponse : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid ReviewAssignmentId { get; private set; }
	public Guid StageId { get; private set; }
	public Guid CriterionId { get; private set; }
	public int? Rating { get; private set; }
	public bool IsNotApplicable { get; private set; }
	public string? Comment { get; private set; }

	protected PerformanceReviewResponse()
	{
	}

	public PerformanceReviewResponse(Guid id, Guid? tenantId, Guid reviewAssignmentId, Guid stageId, Guid criterionId, int? rating, bool isNotApplicable, string? comment) : base(id)
	{
		TenantId = tenantId;
		ReviewAssignmentId = reviewAssignmentId;
		StageId = stageId;
		CriterionId = criterionId;
		Update(rating, isNotApplicable, comment);
	}

	public void Update(int? rating, bool isNotApplicable, string? comment)
	{
		Rating = isNotApplicable ? null : rating;
		IsNotApplicable = isNotApplicable;
		Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
	}
}

public class ClassroomObservation : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid ReviewAssignmentId { get; private set; }
	public Guid ObserverEmployeeId { get; private set; }
	public DateTime ObservationDate { get; private set; }
	public string? ClassName { get; private set; }
	public string? SubjectName { get; private set; }
	public int LessonObjectiveScore { get; private set; }
	public int StudentEngagementScore { get; private set; }
	public int ClassroomManagementScore { get; private set; }
	public int AssessmentScore { get; private set; }
	public string? Notes { get; private set; }

	protected ClassroomObservation()
	{
	}

	public ClassroomObservation(Guid id, Guid? tenantId, Guid reviewAssignmentId, Guid observerEmployeeId, DateTime observationDate, string? className, string? subjectName, int lessonObjectiveScore, int studentEngagementScore, int classroomManagementScore, int assessmentScore, string? notes) : base(id)
	{
		TenantId = tenantId;
		ReviewAssignmentId = reviewAssignmentId;
		ObserverEmployeeId = observerEmployeeId;
		ObservationDate = observationDate.Date;
		ClassName = string.IsNullOrWhiteSpace(className) ? null : className.Trim();
		SubjectName = string.IsNullOrWhiteSpace(subjectName) ? null : subjectName.Trim();
		LessonObjectiveScore = Math.Clamp(lessonObjectiveScore, 1, 5);
		StudentEngagementScore = Math.Clamp(studentEngagementScore, 1, 5);
		ClassroomManagementScore = Math.Clamp(classroomManagementScore, 1, 5);
		AssessmentScore = Math.Clamp(assessmentScore, 1, 5);
		Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
	}
}

public class PerformanceEvidence : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid ReviewAssignmentId { get; private set; }
	public Guid? CriterionId { get; private set; }
	public string Title { get; private set; } = string.Empty;
	public string? FileName { get; private set; }
	public string? Url { get; private set; }
	public string? Notes { get; private set; }

	protected PerformanceEvidence()
	{
	}

	public PerformanceEvidence(Guid id, Guid? tenantId, Guid reviewAssignmentId, Guid? criterionId, string title, string? fileName, string? url, string? notes) : base(id)
	{
		TenantId = tenantId;
		ReviewAssignmentId = reviewAssignmentId;
		CriterionId = criterionId;
		Title = title.Trim();
		FileName = string.IsNullOrWhiteSpace(fileName) ? null : fileName.Trim();
		Url = string.IsNullOrWhiteSpace(url) ? null : url.Trim();
		Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
	}
}

public class PerformanceFeedback : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid ReviewAssignmentId { get; private set; }
	public PerformanceFeedbackSource Source { get; private set; }
	public string? ProviderName { get; private set; }
	public string FeedbackText { get; private set; } = string.Empty;

	protected PerformanceFeedback()
	{
	}

	public PerformanceFeedback(Guid id, Guid? tenantId, Guid reviewAssignmentId, PerformanceFeedbackSource source, string? providerName, string feedbackText) : base(id)
	{
		TenantId = tenantId;
		ReviewAssignmentId = reviewAssignmentId;
		Source = source;
		ProviderName = string.IsNullOrWhiteSpace(providerName) ? null : providerName.Trim();
		FeedbackText = feedbackText.Trim();
	}
}

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

public class DevelopmentPlan : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid ReviewAssignmentId { get; private set; }
	public string Goal { get; private set; } = string.Empty;
	public string? ActionPlan { get; private set; }
	public Guid? OwnerEmployeeId { get; private set; }
	public DateTime? TargetDate { get; private set; }
	public DevelopmentPlanStatus Status { get; private set; }

	protected DevelopmentPlan()
	{
	}

	public DevelopmentPlan(Guid id, Guid? tenantId, Guid reviewAssignmentId, string goal, string? actionPlan, Guid? ownerEmployeeId, DateTime? targetDate) : base(id)
	{
		TenantId = tenantId;
		ReviewAssignmentId = reviewAssignmentId;
		Update(goal, actionPlan, ownerEmployeeId, targetDate, DevelopmentPlanStatus.Open);
	}

	public void Update(string goal, string? actionPlan, Guid? ownerEmployeeId, DateTime? targetDate, DevelopmentPlanStatus status)
	{
		Goal = goal.Trim();
		ActionPlan = string.IsNullOrWhiteSpace(actionPlan) ? null : actionPlan.Trim();
		OwnerEmployeeId = ownerEmployeeId;
		TargetDate = targetDate?.Date;
		Status = status;
	}
}

public class PerformanceAuditEvent : CreationAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid? ReviewAssignmentId { get; private set; }
	public string EventType { get; private set; } = string.Empty;
	public string Summary { get; private set; } = string.Empty;
	public string? MetadataJson { get; private set; }

	protected PerformanceAuditEvent()
	{
	}

	public PerformanceAuditEvent(Guid id, Guid? tenantId, Guid? reviewAssignmentId, string eventType, string summary, string? metadataJson = null) : base(id)
	{
		TenantId = tenantId;
		ReviewAssignmentId = reviewAssignmentId;
		EventType = eventType.Trim();
		Summary = summary.Trim();
		MetadataJson = string.IsNullOrWhiteSpace(metadataJson) ? null : metadataJson.Trim();
	}
}
