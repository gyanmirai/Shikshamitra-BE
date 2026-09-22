using System;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionCycle : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public string Name { get; private set; } = string.Empty;
	public string Code { get; private set; } = string.Empty;
	public string? Description { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public DateTime StartDate { get; private set; }
	public DateTime EndDate { get; private set; }
	public AdmissionCycleStatus Status { get; private set; }
	public DateTime? PublishedAt { get; private set; }
	public Guid? PublishedBy { get; private set; }
	public DateTime? ClosedAt { get; private set; }
	public Guid? ClosedBy { get; private set; }
	public DateTime? ReopenedAt { get; private set; }
	public Guid? ReopenedBy { get; private set; }
	public DateTime? ArchivedAt { get; private set; }
	public Guid? ArchivedBy { get; private set; }
	public DateTime? ScheduledPublishAt { get; private set; }
	public DateTime? ScheduledCloseAt { get; private set; }

	private AdmissionCycle() { }

	public AdmissionCycle(Guid id, Guid? tenantId, string name, string code, Guid academicYearId, DateTime startDate, DateTime endDate,
		string? description = null)
		: base(id)
	{
		TenantId = tenantId;
		UpdateDetails(name, code, academicYearId, startDate, endDate, description);
		Status = AdmissionCycleStatus.Draft;
	}

	public void UpdateDetails(string name, string code, Guid academicYearId, DateTime startDate, DateTime endDate,
		string? description = null)
	{
		if (Status is AdmissionCycleStatus.Published or AdmissionCycleStatus.Closed or AdmissionCycleStatus.Archived)
		{
			throw new BusinessException(AdmissionErrorCodes.CycleCannotBeModified);
		}

		if (endDate.Date <= startDate.Date)
		{
			throw new BusinessException(AdmissionErrorCodes.CycleDateRangeInvalid);
		}

		Name = Check.NotNullOrWhiteSpace(name, nameof(name), AdmissionCycleConsts.MaxNameLength).Trim();
		Code = Check.NotNullOrWhiteSpace(code, nameof(code), AdmissionCycleConsts.MaxCodeLength).Trim().ToUpperInvariant();
		Description = string.IsNullOrWhiteSpace(description)
			? null
			: Check.Length(description.Trim(), nameof(description), AdmissionCycleConsts.MaxDescriptionLength);
		AcademicYearId = Check.NotDefaultOrNull<Guid>(academicYearId, nameof(academicYearId));
		StartDate = startDate.Date;
		EndDate = endDate.Date;
	}

	public void Publish(Guid? publishedBy, DateTime publishedAt)
	{
		if (Status != AdmissionCycleStatus.Draft)
		{
			throw new BusinessException(AdmissionErrorCodes.CycleCannotBeModified);
		}

		Status = AdmissionCycleStatus.Published;
		PublishedBy = publishedBy;
		PublishedAt = publishedAt;
		ScheduledPublishAt = null;
	}

	public void Close(Guid? closedBy = null, DateTime? closedAt = null)
	{
		if (Status != AdmissionCycleStatus.Published)
		{
			throw new BusinessException(AdmissionErrorCodes.CycleCannotBeModified);
		}

		Status = AdmissionCycleStatus.Closed;
		ClosedBy = closedBy;
		ClosedAt = closedAt;
		ScheduledCloseAt = null;
	}

	public void Reopen(Guid? reopenedBy, DateTime reopenedAt)
	{
		if (Status != AdmissionCycleStatus.Closed)
		{
			throw new BusinessException(AdmissionErrorCodes.CycleCannotBeModified);
		}

		Status = AdmissionCycleStatus.Published;
		ReopenedBy = reopenedBy;
		ReopenedAt = reopenedAt;
		ClosedBy = null;
		ClosedAt = null;
	}

	public void ReviseWindow(DateTime startDate, DateTime endDate)
	{
		if (Status is not (AdmissionCycleStatus.Published or AdmissionCycleStatus.Closed))
		{
			throw new BusinessException(AdmissionErrorCodes.CycleCannotBeModified);
		}
		if (endDate.Date <= startDate.Date)
		{
			throw new BusinessException(AdmissionErrorCodes.CycleDateRangeInvalid);
		}

		StartDate = startDate.Date;
		EndDate = endDate.Date;
	}

	public void Archive(Guid? archivedBy = null, DateTime? archivedAt = null)
	{
		if (Status != AdmissionCycleStatus.Closed)
		{
			throw new BusinessException(AdmissionErrorCodes.CycleCannotBeModified);
		}

		Status = AdmissionCycleStatus.Archived;
		ArchivedBy = archivedBy;
		ArchivedAt = archivedAt;
		ScheduledCloseAt = null;
	}

	public void SetSchedule(DateTime? publishAt, DateTime? closeAt)
	{
		if (Status is AdmissionCycleStatus.Closed or AdmissionCycleStatus.Archived)
		{
			throw new BusinessException(AdmissionErrorCodes.CycleCannotBeModified);
		}
		if (Status == AdmissionCycleStatus.Published && publishAt.HasValue)
		{
			throw new BusinessException(AdmissionErrorCodes.CycleScheduleInvalid);
		}
		if (publishAt.HasValue && closeAt.HasValue && closeAt <= publishAt)
		{
			throw new BusinessException(AdmissionErrorCodes.CycleScheduleInvalid);
		}

		ScheduledPublishAt = publishAt?.ToUniversalTime();
		ScheduledCloseAt = closeAt?.ToUniversalTime();
	}
}
