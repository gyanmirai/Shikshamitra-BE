using System;
using ShikshaMitra.HR;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.ProfessionalManagement;

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
