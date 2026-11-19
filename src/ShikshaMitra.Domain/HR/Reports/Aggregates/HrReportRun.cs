using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Reports;

public class HrReportRun : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid OwnerUserId { get; private set; }
	public string Name { get; private set; } = "";
	public string TemplateId { get; private set; } = "";
	public int Revision { get; private set; }
	public string DataSource { get; private set; } = "";
	public string Status { get; private set; } = "Queued";
	public string DefinitionJson { get; private set; } = "{}";
	public string ParametersJson { get; private set; } = "{}";
	public string ScopeJson { get; private set; } = "{}";
	public string ResultJson { get; private set; } = "{}";
	public int RowCount { get; private set; }
	public DateTimeOffset CapturedAt { get; private set; }
	public DateTimeOffset ExpiresAt { get; private set; }
	public string? ErrorMessage { get; private set; }
	public Guid AttemptId { get; private set; }
	public bool ExcelReady { get; private set; }
	public bool PdfReady { get; private set; }
	public string ExcelStatus { get; private set; } = "NotRequested";
	public string PdfStatus { get; private set; } = "NotRequested";
	public bool QueueExport(string format, DateTimeOffset now)
	{
		if (Status != "Completed") throw new BusinessException(HRErrorCodes.ReportRunTransitionInvalid);
		if (format == "excel") { if (ExcelStatus is "Queued" or "Completed") return false; ExcelStatus = "Queued"; }
		else { if (PdfStatus is "Queued" or "Completed") return false; PdfStatus = "Queued"; }
		ExtraProperties["exportQueuedAt_" + format] = now.ToString("O");
		return true;
	}
	public void RecoverInterruptedWork(DateTimeOffset now)
	{
		if (Status == "Running" && CapturedAt < now.AddMinutes(-30)) Fail(AttemptId);
		foreach (var format in new[] { "excel", "pdf" })
			if ((format == "excel" ? ExcelStatus : PdfStatus) == "Queued" && ExtraProperties.TryGetValue("exportQueuedAt_" + format, out var value)
				&& DateTimeOffset.TryParse(value?.ToString(), out var queued) && queued < now.AddMinutes(-30)) FailExport(format);
	}
	public void FailExport(string format, string? message = null)
	{
		if (format == "excel" && ExcelStatus == "Queued") ExcelStatus = "Failed";
		else if (format == "pdf" && PdfStatus == "Queued") PdfStatus = "Failed";
		if (message != null) ExtraProperties["exportError_" + format] = message;
	}
	public void AttachExport(string format)
	{
		if (Status != "Completed") throw new BusinessException(HRErrorCodes.ReportRunTransitionInvalid);
		if (format == "excel") { ExcelReady = true; ExcelStatus = "Completed"; }
		else if (format == "pdf") { PdfReady = true; PdfStatus = "Completed"; }
	}
	private HrReportRun() { }
	public HrReportRun(Guid id, Guid? tenantId, Guid ownerUserId, string name, string templateId, int revision,
		string dataSource, string definitionJson, string parametersJson, string scopeJson, DateTimeOffset now) : base(id)
	{
		TenantId = tenantId; OwnerUserId = ownerUserId; Name = name; TemplateId = templateId; Revision = revision;
		DataSource = dataSource; DefinitionJson = definitionJson; ParametersJson = parametersJson; ScopeJson = scopeJson;
		CapturedAt = now; ExpiresAt = now.AddDays(7); AttemptId = Guid.NewGuid();
	}
	public bool Start(Guid attempt, DateTimeOffset now)
	{
		if (AttemptId != attempt || Status != "Queued" || ExpiresAt <= now) return false;
		Status = "Running"; CapturedAt = now; return true;
	}
	public void Complete(Guid attempt, int rowCount, string resultJson)
	{
		EnsureRunning(attempt); RowCount = rowCount; ResultJson = resultJson; Status = "Completed";
	}
	public void Fail(Guid attempt, string? message = null)
	{
		if (AttemptId != attempt || Status != "Running") return;
		Status = "Failed"; ErrorMessage = message ?? "Report generation failed. Retry the report or narrow its selection.";
	}
	public void Cancel()
	{
		if (Status is not ("Queued" or "Running")) throw new BusinessException(HRErrorCodes.ReportRunTransitionInvalid);
		Status = "Cancelled";
	}
	public void Expire(DateTimeOffset now)
	{
		if (ExpiresAt > now) throw new BusinessException(HRErrorCodes.ReportRunTransitionInvalid);
		Status = "Expired"; ResultJson = "{}"; ExcelReady = false; PdfReady = false;
	}
	private void EnsureRunning(Guid attempt)
	{
		if (AttemptId != attempt || Status != "Running") throw new BusinessException(HRErrorCodes.ReportRunTransitionInvalid);
	}
}