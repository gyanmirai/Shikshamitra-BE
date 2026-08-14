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
	public Guid AcademicYearId { get; private set; }
	public DateTime StartDate { get; private set; }
	public DateTime EndDate { get; private set; }
	public AdmissionCycleStatus Status { get; private set; }
	public DateTime? PublishedAt { get; private set; }
	public Guid? PublishedBy { get; private set; }

	private AdmissionCycle() { }

	public AdmissionCycle(Guid id, Guid? tenantId, string name, string code, Guid academicYearId, DateTime startDate, DateTime endDate)
		: base(id)
	{
		TenantId = tenantId;
		UpdateDetails(name, code, academicYearId, startDate, endDate);
		Status = AdmissionCycleStatus.Draft;
	}

	public void UpdateDetails(string name, string code, Guid academicYearId, DateTime startDate, DateTime endDate)
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
	}

	public void Close() => Status = Status == AdmissionCycleStatus.Published
		? AdmissionCycleStatus.Closed
		: throw new BusinessException(AdmissionErrorCodes.CycleCannotBeModified);

	public void Archive() => Status = Status is AdmissionCycleStatus.Closed or AdmissionCycleStatus.Draft
		? AdmissionCycleStatus.Archived
		: throw new BusinessException(AdmissionErrorCodes.CycleCannotBeModified);
}
