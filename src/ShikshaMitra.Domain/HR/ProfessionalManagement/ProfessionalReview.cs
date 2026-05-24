using System;
using ShikshaMitra.HR;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.ProfessionalManagement;

public class ProfessionalReview : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid CycleId { get; private set; }
	public Guid EmployeeId { get; private set; }
	public Guid? ReviewerUserId { get; private set; }
	public int CommunicationScore { get; private set; }
	public int ProblemSolvingScore { get; private set; }
	public string? Comments { get; private set; }
	public string? Feedback { get; private set; }
	public ProfessionalReviewStatus Status { get; private set; }
	public DateTime? SubmittedAt { get; private set; }

	protected ProfessionalReview()
	{
	}

	public ProfessionalReview(Guid id, Guid? tenantId, Guid cycleId, Guid employeeId, Guid? reviewerUserId) : base(id)
	{
		TenantId = tenantId;
		CycleId = cycleId;
		EmployeeId = employeeId;
		ReviewerUserId = reviewerUserId;
		Status = ProfessionalReviewStatus.Draft;
	}

	public void SaveDraft(int communicationScore, int problemSolvingScore, string? comments, string? feedback, Guid? reviewerUserId)
	{
		if (Status == ProfessionalReviewStatus.Submitted)
		{
			throw new BusinessException(HRErrorCodes.ProfessionalReviewAlreadySubmitted);
		}

		ReviewerUserId = reviewerUserId ?? ReviewerUserId;
		CommunicationScore = communicationScore;
		ProblemSolvingScore = problemSolvingScore;
		Comments = string.IsNullOrWhiteSpace(comments) ? null : comments.Trim();
		Feedback = string.IsNullOrWhiteSpace(feedback) ? null : feedback.Trim();
		Status = ProfessionalReviewStatus.Draft;
		SubmittedAt = null;
	}

	public void Submit(int communicationScore, int problemSolvingScore, string? comments, string? feedback, Guid? reviewerUserId, DateTime submittedAt)
	{
		if (Status == ProfessionalReviewStatus.Submitted)
		{
			throw new BusinessException(HRErrorCodes.ProfessionalReviewAlreadySubmitted);
		}

		SaveDraft(communicationScore, problemSolvingScore, comments, feedback, reviewerUserId);
		Status = ProfessionalReviewStatus.Submitted;
		SubmittedAt = submittedAt;
	}
}
