using System;
using System.Collections.Generic;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

public class AttendancePolicyEvaluationRun : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public Guid AttendanceConfigurationId { get; private set; }
	public int Year { get; private set; }
	public int Month { get; private set; }
	public DateOnly FromDate { get; private set; }
	public DateOnly ToDate { get; private set; }
	public AttendancePolicyEvaluationRunStatus Status { get; private set; }
	public int AffectedEmployeeCount { get; private set; }
	public int EstimatedLateCount { get; private set; }
	public int EstimatedAbsenceCount { get; private set; }
	public int EstimatedOvertimeMinutes { get; private set; }
	public decimal EstimatedPayableDayDelta { get; private set; }
	public string ConfigHash { get; private set; } = string.Empty;
	public string WarningsJson { get; private set; } = "[]";
	public Guid? ApprovedByUserId { get; private set; }
	public DateTime? ApprovedAt { get; private set; }
	public Guid? PublishedByUserId { get; private set; }
	public DateTime? PublishedAt { get; private set; }
	public ICollection<AttendancePolicyEvaluationResult> Results { get; private set; } = new List<AttendancePolicyEvaluationResult>();

	private AttendancePolicyEvaluationRun()
	{
	}

	public AttendancePolicyEvaluationRun(
		Guid id,
		Guid? tenantId,
		Guid academicYearId,
		Guid attendanceConfigurationId,
		int year,
		int month,
		DateOnly fromDate,
		DateOnly toDate,
		string configHash) : base(id)
	{
		if (month is < 1 or > 12)
		{
			throw new BusinessException("ShikshaMitra:AttendanceEvaluationMonthInvalid");
		}

		if (toDate < fromDate)
		{
			throw new BusinessException("ShikshaMitra:AttendanceEvaluationDateRangeInvalid");
		}

		TenantId = tenantId;
		AcademicYearId = academicYearId;
		AttendanceConfigurationId = attendanceConfigurationId;
		Year = year;
		Month = month;
		FromDate = fromDate;
		ToDate = toDate;
		ConfigHash = configHash;
		Status = AttendancePolicyEvaluationRunStatus.Draft;
	}

	public void SetEvaluationSummary(
		int affectedEmployeeCount,
		int estimatedLateCount,
		int estimatedAbsenceCount,
		int estimatedOvertimeMinutes,
		decimal estimatedPayableDayDelta,
		string warningsJson,
		bool hasBlockingWarnings)
	{
		AffectedEmployeeCount = affectedEmployeeCount;
		EstimatedLateCount = estimatedLateCount;
		EstimatedAbsenceCount = estimatedAbsenceCount;
		EstimatedOvertimeMinutes = estimatedOvertimeMinutes;
		EstimatedPayableDayDelta = estimatedPayableDayDelta;
		WarningsJson = warningsJson;
		Status = hasBlockingWarnings
			? AttendancePolicyEvaluationRunStatus.NeedsReview
			: AttendancePolicyEvaluationRunStatus.Evaluated;
	}

	public void Approve(Guid? userId, DateTime approvedAt)
	{
		if (Status != AttendancePolicyEvaluationRunStatus.Evaluated)
		{
			throw new BusinessException("ShikshaMitra:AttendanceEvaluationCannotApprove");
		}

		Status = AttendancePolicyEvaluationRunStatus.Approved;
		ApprovedByUserId = userId;
		ApprovedAt = approvedAt;
	}

	public void Publish(Guid? userId, DateTime publishedAt)
	{
		if (Status != AttendancePolicyEvaluationRunStatus.Approved)
		{
			throw new BusinessException("ShikshaMitra:AttendanceEvaluationCannotPublish");
		}

		Status = AttendancePolicyEvaluationRunStatus.Published;
		PublishedByUserId = userId;
		PublishedAt = publishedAt;
	}

	public void Supersede()
	{
		if (Status == AttendancePolicyEvaluationRunStatus.Published)
		{
			Status = AttendancePolicyEvaluationRunStatus.Superseded;
		}
	}

	public void Cancel()
	{
		if (Status is AttendancePolicyEvaluationRunStatus.Published or AttendancePolicyEvaluationRunStatus.Superseded)
		{
			throw new BusinessException("ShikshaMitra:AttendanceEvaluationCannotCancel");
		}

		Status = AttendancePolicyEvaluationRunStatus.Cancelled;
	}
}
