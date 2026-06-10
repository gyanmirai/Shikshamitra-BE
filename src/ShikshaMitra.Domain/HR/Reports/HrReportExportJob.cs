using System;
using System.Collections.Generic;
using System.Text.Json;
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
	public string RequestJson { get; private set; } = "{}";
	public string Status { get; private set; } = HrReportExportStatus.Queued;
	public string? BlobName { get; private set; }
	public string? ErrorMessage { get; private set; }
	public int ProgressPercent { get; private set; }
	public int? RowCount { get; private set; }
	public long? FileSizeBytes { get; private set; }
	public string SharedUserIdsJson { get; private set; } = "[]";
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
		string requestJson,
		DateTime expiresAt) : base(id)
	{
		TenantId = tenantId;
		RequestedByUserId = requestedByUserId;
		ReportType = reportType;
		Format = format;
		FilterJson = filterJson;
		RequestJson = requestJson;
		ExpiresAt = expiresAt;
	}

	public void MarkRunning(DateTime startedAt)
	{
		Status = HrReportExportStatus.Running;
		StartedAt = startedAt;
		ProgressPercent = 15;
		ErrorMessage = null;
	}

	public void MarkCompleted(string blobName, DateTime completedAt, int rowCount, long fileSizeBytes)
	{
		Status = HrReportExportStatus.Completed;
		BlobName = blobName;
		CompletedAt = completedAt;
		RowCount = rowCount;
		FileSizeBytes = fileSizeBytes;
		ProgressPercent = 100;
		ErrorMessage = null;
	}

	public void MarkFailed(string errorMessage, DateTime completedAt)
	{
		Status = HrReportExportStatus.Failed;
		ErrorMessage = errorMessage;
		CompletedAt = completedAt;
		ProgressPercent = 100;
	}

	public void MarkCancelled(DateTime completedAt)
	{
		Status = HrReportExportStatus.Cancelled;
		CompletedAt = completedAt;
		ProgressPercent = 100;
	}

	public void MarkQueued(DateTime expiresAt)
	{
		Status = HrReportExportStatus.Queued;
		BlobName = null;
		ErrorMessage = null;
		StartedAt = null;
		CompletedAt = null;
		RowCount = null;
		FileSizeBytes = null;
		ProgressPercent = 0;
		ExpiresAt = expiresAt;
	}

	public void ShareWith(IReadOnlyCollection<Guid> userIds)
	{
		SharedUserIdsJson = JsonSerializer.Serialize(userIds);
	}

	public IReadOnlyList<Guid> GetSharedUserIds()
	{
		try
		{
			return JsonSerializer.Deserialize<List<Guid>>(SharedUserIdsJson) ?? [];
		}
		catch
		{
			return [];
		}
	}
}

public static class HrReportExportStatus
{
	public const string Queued = "Queued";
	public const string Running = "Running";
	public const string Completed = "Completed";
	public const string Failed = "Failed";
	public const string Cancelled = "Cancelled";
}
