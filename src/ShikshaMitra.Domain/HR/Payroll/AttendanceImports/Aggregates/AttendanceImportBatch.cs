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
		List<string> warningsSummary) : base(id)
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
}
