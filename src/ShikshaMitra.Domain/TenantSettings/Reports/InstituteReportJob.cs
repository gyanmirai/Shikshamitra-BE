using System;
using System.Text.Json;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.TenantSettings.Reports;

public class InstituteReportJob : AuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid RequestedBy { get; private set; }
	public string RequestJson { get; private set; } = "";
	public string ReportTypeId { get; private set; } = "";
	public DateTime? StartedAt { get; private set; }
	public DateTime? LeaseExpiresAt { get; private set; }
	public Guid? LeaseId { get; private set; }
	public int Attempts { get; private set; }
	public bool IsDynamic { get; private set; }
	public string Status { get; private set; } = "Queued";
	public string? BlobName { get; private set; }
	public string? ErrorMessage { get; private set; }
	public DateTime? CompletedAt { get; private set; }
	public DateTime ExpiresAt { get; private set; }
	private InstituteReportJob() { }
	public InstituteReportJob(Guid id, Guid tenantId, Guid requestedBy, string requestJson, bool dynamic) : base(id)
	{
		TenantId = tenantId;
		RequestedBy = requestedBy;
		RequestJson = Check.NotNullOrWhiteSpace(requestJson, nameof(requestJson));
		using var request = JsonDocument.Parse(requestJson);
		ReportTypeId = request.RootElement.TryGetProperty("ReportType", out var type) ? type.GetString() ?? "" : "";
		IsDynamic = dynamic;
		ExpiresAt = DateTime.UtcNow.AddDays(7);
	}
	public void Start()
	{
		if (Status != "Queued") throw new BusinessException("InstituteReports:JobAlreadyStarted");
		Status = "Processing";
		StartedAt = DateTime.UtcNow; LeaseExpiresAt = StartedAt.Value.AddMinutes(10); LeaseId = Guid.NewGuid(); Attempts++;
	}
	public void Complete(string blob)
	{
		if (Status != "Processing") throw new BusinessException("InstituteReports:JobNotRunning");
		BlobName = blob; Status = "Completed"; CompletedAt = DateTime.UtcNow; LeaseExpiresAt = null; LeaseId = null;
	}
	public void Fail() { Status = "Failed"; ErrorMessage = "Report generation failed. Generate the report again or contact your administrator."; CompletedAt = DateTime.UtcNow; LeaseExpiresAt = null; LeaseId = null; }
	public void RemoveExpiredFile() { BlobName = null; }
}
