using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmReportSchedule : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public string Name { get; private set; } = string.Empty;
	public AdmissionCrmExportReportType ReportType { get; private set; }
	public AdmissionCrmReportFrequency Frequency { get; private set; }
	public string Destination { get; private set; } = string.Empty;
	public string FiltersJson { get; private set; } = "{}";
	public DateTime NextRunAt { get; private set; }
	public bool IsActive { get; private set; }
	public Guid CreatedByUserId { get; private set; }
	public DateTime? LastRunAt { get; private set; }

	protected AdmissionCrmReportSchedule() { }
	public AdmissionCrmReportSchedule(Guid id, Guid? tenantId, Guid academicYearId, string name,
		AdmissionCrmExportReportType reportType, AdmissionCrmReportFrequency frequency, string destination,
		string filtersJson, DateTime nextRunAt, Guid userId) : base(id)
	{
		TenantId = tenantId; AcademicYearId = academicYearId;
		Name = Check.NotNullOrWhiteSpace(name, nameof(name), AdmissionCrmConsts.MaxNameLength);
		ReportType = reportType; Frequency = frequency;
		Destination = Check.NotNullOrWhiteSpace(destination, nameof(destination), AdmissionCrmConsts.MaxNotesLength);
		FiltersJson = Check.NotNullOrWhiteSpace(filtersJson, nameof(filtersJson), AdmissionCrmConsts.MaxActivationStepsJsonLength);
		NextRunAt = nextRunAt.ToUniversalTime(); CreatedByUserId = userId; IsActive = true;
	}
	public void SetActive(bool active) => IsActive = active;
	public void MarkRun(DateTime at)
	{
		LastRunAt = at.ToUniversalTime();
		NextRunAt = Frequency switch
		{
			AdmissionCrmReportFrequency.Daily => LastRunAt.Value.AddDays(1),
			AdmissionCrmReportFrequency.Weekly => LastRunAt.Value.AddDays(7),
			_ => LastRunAt.Value.AddMonths(1)
		};
	}
}
