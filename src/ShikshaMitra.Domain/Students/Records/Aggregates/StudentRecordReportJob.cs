using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.Records;

public class StudentRecordReportJob : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid RequestedBy { get; private set; }
	public string ClientKey { get; private set; } = string.Empty;
	public string ReportType { get; private set; } = string.Empty;
	public string RequestJson { get; private set; } = "{}";
	public string AuthorityHash { get; private set; } = string.Empty;
	public string Status { get; private set; } = "Queued";
	public string? BlobName { get; private set; }
	public string? ErrorCode { get; private set; }
	public DateTimeOffset ExpiresAt { get; private set; }
	public DateTimeOffset? LeaseUntil { get; private set; }
	public Guid? GenerationId { get; private set; }
	public int? RowCount { get; private set; }
	protected StudentRecordReportJob() { }
	public StudentRecordReportJob(Guid id, Guid tenantId, Guid userId, string key, string type, string requestJson, string authorityHash,
		DateTimeOffset expires) : base(id)
	{
		TenantId = tenantId; RequestedBy = userId; ClientKey = Check.NotNullOrWhiteSpace(key, nameof(key), 80);
		ReportType = Check.NotNullOrWhiteSpace(type, nameof(type), 40); RequestJson = requestJson;
		AuthorityHash = authorityHash; ExpiresAt = expires;
	}
	public void Start(Guid generation, DateTimeOffset now)
	{
		if (Status != "Queued" && !(Status == "Generating" && LeaseUntil <= now)) throw new BusinessException("Students:Records:ReportState");
		Status = "Generating"; GenerationId = generation; LeaseUntil = now.AddMinutes(20);
	}
	public void Complete(Guid generation, string blob, int rows)
	{
		EnsureGeneration(generation); Status = "Completed"; BlobName = blob; RowCount = rows; LeaseUntil = null; ErrorCode = null;
	}
	public void Fail(Guid generation, string code)
	{
		EnsureGeneration(generation); Status = "Failed"; ErrorCode = code; LeaseUntil = null;
	}
	public void Cancel()
	{
		if (Status is not ("Queued" or "Generating")) throw new BusinessException("Students:Records:ReportState");
		Status = "Cancelled"; GenerationId = null; LeaseUntil = null;
	}
	private void EnsureGeneration(Guid id)
	{
		if (Status != "Generating" || GenerationId != id) throw new BusinessException("Students:Records:ReportState");
	}
}
