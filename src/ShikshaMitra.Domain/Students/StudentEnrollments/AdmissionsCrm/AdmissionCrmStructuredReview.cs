using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmReviewConfiguration : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	private readonly List<AdmissionCrmReviewConfigurationCriterion> _criteria = [];

	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public bool IsEnabled { get; private set; }
	public string Title { get; private set; } = string.Empty;
	public int RequiredCompletedReviews { get; private set; }
	public bool RequireConflictDeclaration { get; private set; }
	public int Version { get; private set; }
	public IReadOnlyCollection<AdmissionCrmReviewConfigurationCriterion> Criteria => _criteria;

	protected AdmissionCrmReviewConfiguration() { }

	public AdmissionCrmReviewConfiguration(Guid id, Guid? tenantId, Guid academicYearId) : base(id)
	{
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		Title = "Admissions committee review";
		RequiredCompletedReviews = 2;
		RequireConflictDeclaration = true;
		Version = 0;
	}

	public void Update(bool isEnabled, string title, int requiredCompletedReviews,
		bool requireConflictDeclaration, IReadOnlyCollection<AdmissionCrmReviewCriterionInput> criteria,
		Func<Guid> idFactory)
	{
		if (requiredCompletedReviews is < 1 or > 10)
			throw new BusinessException("AdmissionCrm:InvalidRequiredReviewCount");
		if (criteria.Count is < 1 or > 20)
			throw new BusinessException("AdmissionCrm:InvalidReviewCriterionCount");
		var normalizedKeys = criteria.Select(x => NormalizeKey(x.Key)).ToList();
		if (normalizedKeys.Distinct(StringComparer.OrdinalIgnoreCase).Count() != normalizedKeys.Count)
			throw new BusinessException("AdmissionCrm:DuplicateReviewCriterionKey");

		Title = Check.NotNullOrWhiteSpace(title, nameof(title), AdmissionCrmConsts.MaxDescriptionLength);
		IsEnabled = isEnabled;
		RequiredCompletedReviews = requiredCompletedReviews;
		RequireConflictDeclaration = requireConflictDeclaration;
		_criteria.Clear();
		var order = 0;
		foreach (var criterion in criteria)
		{
			_criteria.Add(new AdmissionCrmReviewConfigurationCriterion(idFactory(), TenantId, Id,
				normalizedKeys[order], criterion.Label, criterion.MaximumScore, criterion.IsRequired, order));
			order++;
		}
		Version++;
	}

	private static string NormalizeKey(string key) =>
		Check.NotNullOrWhiteSpace(key, nameof(key), 64).Trim().ToLowerInvariant();
}

public sealed record AdmissionCrmReviewCriterionInput(
	string Key, string Label, int MaximumScore, bool IsRequired);

public class AdmissionCrmReviewConfigurationCriterion : FullAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid ConfigurationId { get; private set; }
	public string Key { get; private set; } = string.Empty;
	public string Label { get; private set; } = string.Empty;
	public int MaximumScore { get; private set; }
	public bool IsRequired { get; private set; }
	public int DisplayOrder { get; private set; }

	protected AdmissionCrmReviewConfigurationCriterion() { }

	internal AdmissionCrmReviewConfigurationCriterion(Guid id, Guid? tenantId, Guid configurationId,
		string key, string label, int maximumScore, bool isRequired, int displayOrder) : base(id)
	{
		if (maximumScore is < 1 or > 100)
			throw new BusinessException("AdmissionCrm:InvalidReviewCriterionMaximumScore");
		TenantId = tenantId;
		ConfigurationId = configurationId;
		Key = key;
		Label = Check.NotNullOrWhiteSpace(label, nameof(label), AdmissionCrmConsts.MaxDescriptionLength);
		MaximumScore = maximumScore;
		IsRequired = isRequired;
		DisplayOrder = displayOrder;
	}
}

public class AdmissionCrmStructuredReview : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	private readonly List<AdmissionCrmStructuredReviewCriterion> _criteria = [];
	private readonly List<AdmissionCrmReviewAssignment> _assignments = [];

	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public Guid LeadId { get; private set; }
	public Guid ConfigurationId { get; private set; }
	public int ConfigurationVersion { get; private set; }
	public string Title { get; private set; } = string.Empty;
	public int RequiredCompletedReviews { get; private set; }
	public bool RequireConflictDeclaration { get; private set; }
	public AdmissionCrmStructuredReviewStatus Status { get; private set; }
	public DateTime StartedAt { get; private set; }
	public DateTime? ReadyAt { get; private set; }
	public DateTime? ClosedAt { get; private set; }
	public Guid? ClosedByUserId { get; private set; }
	public string? ClosureReason { get; private set; }
	public IReadOnlyCollection<AdmissionCrmStructuredReviewCriterion> Criteria => _criteria;
	public IReadOnlyCollection<AdmissionCrmReviewAssignment> Assignments => _assignments;
	public int SubmittedReviewCount => _assignments.Count(x => x.Status == AdmissionCrmReviewAssignmentStatus.Submitted);

	protected AdmissionCrmStructuredReview() { }

	public AdmissionCrmStructuredReview(Guid id, Guid? tenantId, Guid academicYearId, Guid leadId,
		AdmissionCrmReviewConfiguration configuration, DateTime startedAt, Func<Guid> idFactory) : base(id)
	{
		if (!configuration.IsEnabled)
			throw new BusinessException("AdmissionCrm:StructuredReviewDisabled");
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		LeadId = leadId;
		ConfigurationId = configuration.Id;
		ConfigurationVersion = configuration.Version;
		Title = configuration.Title;
		RequiredCompletedReviews = configuration.RequiredCompletedReviews;
		RequireConflictDeclaration = configuration.RequireConflictDeclaration;
		StartedAt = startedAt.ToUniversalTime();
		Status = AdmissionCrmStructuredReviewStatus.InReview;
		foreach (var criterion in configuration.Criteria.OrderBy(x => x.DisplayOrder))
			_criteria.Add(new AdmissionCrmStructuredReviewCriterion(idFactory(), TenantId, Id,
				criterion.Key, criterion.Label, criterion.MaximumScore, criterion.IsRequired, criterion.DisplayOrder));
	}

	public AdmissionCrmReviewAssignment Assign(Guid id, Guid reviewerUserId, DateTime dueAt, DateTime assignedAt)
	{
		EnsureOpen();
		if (dueAt <= assignedAt)
			throw new BusinessException("AdmissionCrm:ReviewDueDateMustBeFuture");
		if (_assignments.Any(x => x.ReviewerUserId == reviewerUserId &&
			x.Status is AdmissionCrmReviewAssignmentStatus.Assigned or AdmissionCrmReviewAssignmentStatus.Submitted))
			throw new BusinessException("AdmissionCrm:ReviewerAlreadyAssigned");
		var assignment = new AdmissionCrmReviewAssignment(id, TenantId, Id, reviewerUserId, dueAt, assignedAt);
		_assignments.Add(assignment);
		return assignment;
	}

	public void DeclareConflict(Guid assignmentId, Guid actorUserId, bool hasConflict, string? reason, DateTime at)
	{
		var assignment = GetAssignment(assignmentId);
		assignment.DeclareConflict(actorUserId, hasConflict, reason, at);
	}

	public void Submit(Guid assignmentId, Guid actorUserId,
		AdmissionCrmReviewRecommendation recommendation, string confidentialRecommendation,
		IReadOnlyDictionary<string, int?> scores, DateTime submittedAt)
	{
		EnsureOpen();
		var assignment = GetAssignment(assignmentId);
		if (RequireConflictDeclaration && assignment.ConflictDeclaredAt == null)
			throw new BusinessException("AdmissionCrm:ReviewConflictDeclarationRequired");
		var normalized = scores.ToDictionary(x => x.Key.Trim().ToLowerInvariant(), x => x.Value);
		foreach (var criterion in _criteria)
		{
			if (!normalized.TryGetValue(criterion.Key, out var score) || score == null)
			{
				if (criterion.IsRequired)
					throw new BusinessException("AdmissionCrm:RequiredReviewScoreMissing").WithData("Criterion", criterion.Key);
				continue;
			}
			if (score < 0 || score > criterion.MaximumScore)
				throw new BusinessException("AdmissionCrm:ReviewScoreOutOfRange").WithData("Criterion", criterion.Key);
		}
		assignment.Submit(actorUserId, recommendation, confidentialRecommendation,
			JsonSerializer.Serialize(normalized), submittedAt);
		if (SubmittedReviewCount >= RequiredCompletedReviews)
		{
			Status = AdmissionCrmStructuredReviewStatus.ReadyForDecision;
			ReadyAt ??= submittedAt.ToUniversalTime();
		}
	}

	public void CancelAssignment(Guid assignmentId, DateTime at, string reason)
	{
		EnsureOpen();
		GetAssignment(assignmentId).Cancel(at, reason);
	}

	public void Close(Guid actorUserId, DateTime at, string reason)
	{
		if (Status != AdmissionCrmStructuredReviewStatus.ReadyForDecision)
			throw new BusinessException("AdmissionCrm:StructuredReviewNotReady");
		Status = AdmissionCrmStructuredReviewStatus.Closed;
		ClosedAt = at.ToUniversalTime();
		ClosedByUserId = actorUserId;
		ClosureReason = Check.NotNullOrWhiteSpace(reason, nameof(reason), AdmissionCrmConsts.MaxNotesLength);
	}

	private AdmissionCrmReviewAssignment GetAssignment(Guid id) => _assignments.SingleOrDefault(x => x.Id == id)
		?? throw new BusinessException("AdmissionCrm:ReviewAssignmentNotFound");

	private void EnsureOpen()
	{
		if (Status is AdmissionCrmStructuredReviewStatus.Closed or AdmissionCrmStructuredReviewStatus.Cancelled)
			throw new BusinessException("AdmissionCrm:StructuredReviewClosed");
	}
}

public class AdmissionCrmStructuredReviewCriterion : FullAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid ReviewId { get; private set; }
	public string Key { get; private set; } = string.Empty;
	public string Label { get; private set; } = string.Empty;
	public int MaximumScore { get; private set; }
	public bool IsRequired { get; private set; }
	public int DisplayOrder { get; private set; }
	protected AdmissionCrmStructuredReviewCriterion() { }
	internal AdmissionCrmStructuredReviewCriterion(Guid id, Guid? tenantId, Guid reviewId, string key,
		string label, int maximumScore, bool isRequired, int displayOrder) : base(id)
	{
		TenantId = tenantId; ReviewId = reviewId; Key = key; Label = label;
		MaximumScore = maximumScore; IsRequired = isRequired; DisplayOrder = displayOrder;
	}
}

public class AdmissionCrmReviewAssignment : FullAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid ReviewId { get; private set; }
	public Guid ReviewerUserId { get; private set; }
	public DateTime DueAt { get; private set; }
	public AdmissionCrmReviewAssignmentStatus Status { get; private set; }
	public DateTime AssignedAt { get; private set; }
	public bool? HasDeclaredConflict { get; private set; }
	public string? ConflictReason { get; private set; }
	public DateTime? ConflictDeclaredAt { get; private set; }
	public AdmissionCrmReviewRecommendation? Recommendation { get; private set; }
	public string? ConfidentialRecommendation { get; private set; }
	public string? ScoresJson { get; private set; }
	public DateTime? SubmittedAt { get; private set; }
	public DateTime? CancelledAt { get; private set; }
	public string? CancellationReason { get; private set; }

	protected AdmissionCrmReviewAssignment() { }
	internal AdmissionCrmReviewAssignment(Guid id, Guid? tenantId, Guid reviewId, Guid reviewerUserId,
		DateTime dueAt, DateTime assignedAt) : base(id)
	{
		TenantId = tenantId; ReviewId = reviewId; ReviewerUserId = reviewerUserId;
		DueAt = dueAt.ToUniversalTime(); AssignedAt = assignedAt.ToUniversalTime();
		Status = AdmissionCrmReviewAssignmentStatus.Assigned;
	}

	internal void DeclareConflict(Guid actorUserId, bool hasConflict, string? reason, DateTime at)
	{
		EnsureActor(actorUserId);
		if (Status != AdmissionCrmReviewAssignmentStatus.Assigned)
			throw new BusinessException("AdmissionCrm:ReviewAssignmentNotOpen");
		if (hasConflict && string.IsNullOrWhiteSpace(reason))
			throw new BusinessException("AdmissionCrm:ReviewConflictReasonRequired");
		HasDeclaredConflict = hasConflict;
		ConflictReason = string.IsNullOrWhiteSpace(reason) ? null : Check.Length(reason.Trim(), nameof(reason), AdmissionCrmConsts.MaxNotesLength);
		ConflictDeclaredAt = at.ToUniversalTime();
		if (hasConflict) Status = AdmissionCrmReviewAssignmentStatus.Recused;
	}

	internal void Submit(Guid actorUserId, AdmissionCrmReviewRecommendation recommendation,
		string confidentialRecommendation, string scoresJson, DateTime at)
	{
		EnsureActor(actorUserId);
		if (Status != AdmissionCrmReviewAssignmentStatus.Assigned || HasDeclaredConflict == true)
			throw new BusinessException("AdmissionCrm:ReviewAssignmentNotOpen");
		Recommendation = recommendation;
		ConfidentialRecommendation = Check.NotNullOrWhiteSpace(confidentialRecommendation,
			nameof(confidentialRecommendation), AdmissionCrmConsts.MaxNotesLength);
		ScoresJson = scoresJson;
		SubmittedAt = at.ToUniversalTime();
		Status = AdmissionCrmReviewAssignmentStatus.Submitted;
	}

	internal void Cancel(DateTime at, string reason)
	{
		if (Status != AdmissionCrmReviewAssignmentStatus.Assigned)
			throw new BusinessException("AdmissionCrm:ReviewAssignmentNotOpen");
		Status = AdmissionCrmReviewAssignmentStatus.Cancelled;
		CancelledAt = at.ToUniversalTime();
		CancellationReason = Check.NotNullOrWhiteSpace(reason, nameof(reason), AdmissionCrmConsts.MaxNotesLength);
	}

	private void EnsureActor(Guid actorUserId)
	{
		if (actorUserId != ReviewerUserId)
			throw new BusinessException("AdmissionCrm:ReviewAssignmentActorMismatch");
	}
}
