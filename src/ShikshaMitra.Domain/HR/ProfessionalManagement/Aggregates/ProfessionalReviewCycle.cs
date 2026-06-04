using System;
using ShikshaMitra.HR;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.ProfessionalManagement;

public class ProfessionalReviewCycle : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid? AcademicYearId { get; private set; }
	public string Name { get; private set; } = string.Empty;
	public PerformanceCycleType CycleType { get; private set; }
	public DateTime PeriodStart { get; private set; }
	public DateTime PeriodEnd { get; private set; }
	public DateTime? SelfReviewDueDate { get; private set; }
	public DateTime? ReviewerDueDate { get; private set; }
	public DateTime? FinalizationDueDate { get; private set; }
	public int RatingScaleMax { get; private set; } = 5;
	public bool AllowNotApplicable { get; private set; } = true;
	public string? AudienceFilterJson { get; private set; }
	public string? ReviewerWorkflowJson { get; private set; }
	public ProfessionalReviewCycleStatus Status { get; private set; }

	protected ProfessionalReviewCycle()
	{
	}

	public ProfessionalReviewCycle(
		Guid id,
		Guid? tenantId,
		string name,
		DateTime periodStart,
		DateTime periodEnd,
		ProfessionalReviewCycleStatus status) : base(id)
	{
		TenantId = tenantId;
		Update(name, periodStart, periodEnd, status);
	}

	public ProfessionalReviewCycle(
		Guid id,
		Guid? tenantId,
		Guid? academicYearId,
		string name,
		PerformanceCycleType cycleType,
		DateTime periodStart,
		DateTime periodEnd,
		DateTime? selfReviewDueDate,
		DateTime? reviewerDueDate,
		DateTime? finalizationDueDate,
		int ratingScaleMax,
		bool allowNotApplicable,
		string? audienceFilterJson,
		string? reviewerWorkflowJson,
		ProfessionalReviewCycleStatus status) : base(id)
	{
		TenantId = tenantId;
		Update(
			academicYearId,
			name,
			cycleType,
			periodStart,
			periodEnd,
			selfReviewDueDate,
			reviewerDueDate,
			finalizationDueDate,
			ratingScaleMax,
			allowNotApplicable,
			audienceFilterJson,
			reviewerWorkflowJson,
			status);
	}

	public void Update(
		string name,
		DateTime periodStart,
		DateTime periodEnd,
		ProfessionalReviewCycleStatus status)
	{
		Update(AcademicYearId, name, CycleType, periodStart, periodEnd, SelfReviewDueDate, ReviewerDueDate, FinalizationDueDate, RatingScaleMax, AllowNotApplicable, AudienceFilterJson, ReviewerWorkflowJson, status);
	}

	public void Update(
		Guid? academicYearId,
		string name,
		PerformanceCycleType cycleType,
		DateTime periodStart,
		DateTime periodEnd,
		DateTime? selfReviewDueDate,
		DateTime? reviewerDueDate,
		DateTime? finalizationDueDate,
		int ratingScaleMax,
		bool allowNotApplicable,
		string? audienceFilterJson,
		string? reviewerWorkflowJson,
		ProfessionalReviewCycleStatus status)
	{
		if (Status == ProfessionalReviewCycleStatus.Finalized || Status == ProfessionalReviewCycleStatus.Archived)
		{
			throw new BusinessException(HRErrorCodes.PerformanceReviewFinalized);
		}

		if (periodEnd.Date < periodStart.Date)
		{
			throw new BusinessException(HRErrorCodes.ProfessionalReviewCycleInvalidPeriod);
		}

		AcademicYearId = academicYearId;
		Name = name.Trim();
		CycleType = cycleType;
		PeriodStart = periodStart.Date;
		PeriodEnd = periodEnd.Date;
		SelfReviewDueDate = selfReviewDueDate?.Date;
		ReviewerDueDate = reviewerDueDate?.Date;
		FinalizationDueDate = finalizationDueDate?.Date;
		RatingScaleMax = Math.Clamp(ratingScaleMax, 1, 10);
		AllowNotApplicable = allowNotApplicable;
		AudienceFilterJson = string.IsNullOrWhiteSpace(audienceFilterJson) ? null : audienceFilterJson.Trim();
		ReviewerWorkflowJson = string.IsNullOrWhiteSpace(reviewerWorkflowJson) ? null : reviewerWorkflowJson.Trim();
		Status = status;
	}

	public void Publish()
	{
		if (Status == ProfessionalReviewCycleStatus.Finalized || Status == ProfessionalReviewCycleStatus.Archived)
		{
			throw new BusinessException(HRErrorCodes.PerformanceReviewFinalized);
		}

		Status = ProfessionalReviewCycleStatus.Published;
	}

	public void MoveToCalibration()
	{
		if (Status == ProfessionalReviewCycleStatus.Finalized || Status == ProfessionalReviewCycleStatus.Archived)
		{
			throw new BusinessException(HRErrorCodes.PerformanceReviewFinalized);
		}

		if (Status is not (ProfessionalReviewCycleStatus.Published or ProfessionalReviewCycleStatus.InProgress))
		{
			throw new BusinessException(HRErrorCodes.PerformanceReviewCycleInvalidStatus);
		}

		Status = ProfessionalReviewCycleStatus.Calibration;
	}

	public void MarkInProgress()
	{
		if (Status == ProfessionalReviewCycleStatus.Published)
		{
			Status = ProfessionalReviewCycleStatus.InProgress;
		}
	}

	public void EnsureCanAssign()
	{
		if (Status is not (ProfessionalReviewCycleStatus.Published or ProfessionalReviewCycleStatus.InProgress))
		{
			throw new BusinessException(HRErrorCodes.PerformanceReviewCycleInvalidStatus);
		}
	}

	public void FinalizeCycle()
	{
		if (Status != ProfessionalReviewCycleStatus.Calibration)
		{
			throw new BusinessException(HRErrorCodes.PerformanceReviewCycleInvalidStatus);
		}

		Status = ProfessionalReviewCycleStatus.Finalized;
	}

	public void Archive()
	{
		Status = ProfessionalReviewCycleStatus.Archived;
	}
}
