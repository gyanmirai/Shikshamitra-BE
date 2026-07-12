using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmExportRequest : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public AdmissionCrmExportReportType ReportType { get; private set; }
	public string DefinitionVersion { get; private set; } = "1";
	public string FiltersJson { get; private set; } = "{}";
	public string FileName { get; private set; } = string.Empty;
	public string ContentType { get; private set; } = "text/csv";
	public string Content { get; private set; } = string.Empty;
	public AdmissionCrmExportStatus Status { get; private set; }
	public Guid RequestedByUserId { get; private set; }
	public DateTime RequestedAt { get; private set; }
	public DateTime DataAsOf { get; private set; }
	public DateTime ExpiresAt { get; private set; }
	public int DownloadCount { get; private set; }
	public DateTime? LastDownloadedAt { get; private set; }
	public Guid? LastDownloadedByUserId { get; private set; }

	protected AdmissionCrmExportRequest() { }
	public AdmissionCrmExportRequest(Guid id, Guid? tenantId, Guid academicYearId,
		AdmissionCrmExportReportType reportType, string filtersJson, string fileName, string content,
		Guid requestedByUserId, DateTime at, DateTime expiresAt) : base(id)
	{
		TenantId = tenantId; AcademicYearId = academicYearId; ReportType = reportType;
		FiltersJson = Check.NotNullOrWhiteSpace(filtersJson, nameof(filtersJson), AdmissionCrmConsts.MaxActivationStepsJsonLength);
		FileName = Check.NotNullOrWhiteSpace(fileName, nameof(fileName), AdmissionCrmConsts.MaxNameLength);
		Content = Check.NotNullOrWhiteSpace(content, nameof(content), 10_000_000);
		RequestedByUserId = requestedByUserId; RequestedAt = at.ToUniversalTime(); DataAsOf = RequestedAt;
		ExpiresAt = expiresAt.ToUniversalTime(); Status = AdmissionCrmExportStatus.Ready;
	}
	public void RegisterDownload(Guid userId, DateTime at)
	{
		if (Status != AdmissionCrmExportStatus.Ready || at.ToUniversalTime() > ExpiresAt)
		{
			Status = AdmissionCrmExportStatus.Expired;
			throw new BusinessException("AdmissionCrm:ExportExpired");
		}
		DownloadCount++; LastDownloadedAt = at.ToUniversalTime(); LastDownloadedByUserId = userId;
	}
	public void Revoke() => Status = AdmissionCrmExportStatus.Revoked;
	public bool ExpireContent(DateTime at)
	{
		if (Status != AdmissionCrmExportStatus.Ready || at.ToUniversalTime() <= ExpiresAt) return false;
		Status = AdmissionCrmExportStatus.Expired;
		Content = "Expired and removed.";
		return true;
	}
}
