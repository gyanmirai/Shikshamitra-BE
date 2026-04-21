using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Payroll.AttendanceImports;

public class AttendanceImportBatch : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public string FileName { get; private set; } = string.Empty;
	public Guid ImportedByUserId { get; private set; }
	public string ImportedByName { get; private set; } = string.Empty;
	public DateTime ImportedAt { get; private set; }
	public string? PayrollMonthYear { get; private set; }
	public string? PayrollMonth { get; private set; }
	public AttendanceImportBatchStatus Status { get; private set; }
	public int TotalRows { get; private set; }
	public int ValidRows { get; private set; }
	public int InvalidRows { get; private set; }
	public int CreateCount { get; private set; }
	public int ReplaceCount { get; private set; }
	public int ProcessedRows { get; private set; }
	public int CreatedCount { get; private set; }
	public int ReplacedCount { get; private set; }
	public string? PreviewToken { get; private set; }
	public DateTime? PreviewTokenExpiresAt { get; private set; }
	public DateTime? ConfirmedAt { get; private set; }
	public List<string> ErrorsSummary { get; private set; } = [];
	public List<string> WarningsSummary { get; private set; } = [];

	/// <summary>How this batch was produced (file vs integration job vs internal DB load).</summary>
	public AttendanceImportRunSource RunSource { get; private set; }

	/// <summary>Optional id from an external system or job run for correlation.</summary>
	public string? ExternalCorrelationId { get; private set; }

	/// <summary>Optional provider or integration name.</summary>
	public string? IntegrationProvider { get; private set; }

	public DateTime? RunStartedAt { get; private set; }
	public DateTime? RunFinishedAt { get; private set; }

	/// <summary>When true, this batch should not be edited or reprocessed (separate from payroll finalization).</summary>
	public bool IsBatchLocked { get; private set; }

	private AttendanceImportBatch() { }

	public AttendanceImportBatch(
		Guid id,
		Guid? tenantId,
		string fileName,
		Guid importedByUserId,
		string importedByName,
		DateTime importedAt,
		string? payrollMonthYear,
		string? payrollMonth,
		int totalRows,
		int validRows,
		int invalidRows,
		int createCount,
		int replaceCount,
		List<string> errorsSummary,
		List<string> warningsSummary,
		AttendanceImportRunSource runSource = AttendanceImportRunSource.FileUpload,
		string? externalCorrelationId = null,
		string? integrationProvider = null,
		DateTime? runStartedAt = null,
		DateTime? runFinishedAt = null,
		bool isBatchLocked = false) : base(id)
	{
		TenantId = tenantId;
		FileName = fileName;
		ImportedByUserId = importedByUserId;
		ImportedByName = importedByName;
		ImportedAt = importedAt;
		PayrollMonthYear = payrollMonthYear;
		PayrollMonth = payrollMonth;
		Status = AttendanceImportBatchStatus.Preview;
		TotalRows = totalRows;
		ValidRows = validRows;
		InvalidRows = invalidRows;
		CreateCount = createCount;
		ReplaceCount = replaceCount;
		ErrorsSummary = errorsSummary;
		WarningsSummary = warningsSummary;
		RunSource = runSource;
		ExternalCorrelationId = externalCorrelationId;
		IntegrationProvider = integrationProvider;
		RunStartedAt = runStartedAt;
		RunFinishedAt = runFinishedAt;
		IsBatchLocked = isBatchLocked;
	}

	public void SetPreviewToken(string previewToken, DateTime previewTokenExpiresAt)
	{
		PreviewToken = previewToken;
		PreviewTokenExpiresAt = previewTokenExpiresAt;
	}

	public void MarkConfirmed(
		int processedRows,
		int createdCount,
		int replacedCount,
		DateTime confirmedAt)
	{
		Status = AttendanceImportBatchStatus.Confirmed;
		ProcessedRows = processedRows;
		CreatedCount = createdCount;
		ReplacedCount = replacedCount;
		ConfirmedAt = confirmedAt;
		PreviewToken = null;
		PreviewTokenExpiresAt = null;
	}

	public void SetBatchLocked(bool locked)
	{
		IsBatchLocked = locked;
	}

	public void SetRunTiming(DateTime? startedAt, DateTime? finishedAt)
	{
		RunStartedAt = startedAt;
		RunFinishedAt = finishedAt;
	}
}
