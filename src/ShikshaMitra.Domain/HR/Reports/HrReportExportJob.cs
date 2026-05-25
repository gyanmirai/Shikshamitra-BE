using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Reports;

public class HrReportExportJob : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid RequestedByUserId { get; private set; }
	public string ReportType { get; private set; } = string.Empty;
	public string Format { get; private set; } = string.Empty;
	public string FilterJson { get; private set; } = "{}";
	public string Status { get; private set; } = HrReportExportStatus.Queued;
	public string? BlobName { get; private set; }
	public string? ErrorMessage { get; private set; }
	public DateTime? StartedAt { get; private set; }
	public DateTime? CompletedAt { get; private set; }
	public DateTime ExpiresAt { get; private set; }

	private HrReportExportJob()
	{
	}

	public HrReportExportJob(
		Guid id,
		Guid? tenantId,
		Guid requestedByUserId,
		string reportType,
		string format,
		string filterJson,
		DateTime expiresAt) : base(id)
	{
		TenantId = tenantId;
		RequestedByUserId = requestedByUserId;
		ReportType = reportType;
		Format = format;
		FilterJson = filterJson;
		ExpiresAt = expiresAt;
	}

	public void MarkRunning(DateTime startedAt)
	{
		Status = HrReportExportStatus.Running;
		StartedAt = startedAt;
		ErrorMessage = null;
	}

	public void MarkCompleted(string blobName, DateTime completedAt)
	{
		Status = HrReportExportStatus.Completed;
		BlobName = blobName;
		CompletedAt = completedAt;
		ErrorMessage = null;
	}

	public void MarkFailed(string errorMessage, DateTime completedAt)
	{
		Status = HrReportExportStatus.Failed;
		ErrorMessage = errorMessage;
		CompletedAt = completedAt;
	}
}

public static class HrReportExportStatus
{
	public const string Queued = "Queued";
	public const string Running = "Running";
	public const string Completed = "Completed";
	public const string Failed = "Failed";
}
