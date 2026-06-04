using System;
using ShikshaMitra.HR;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.ProfessionalManagement;

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
