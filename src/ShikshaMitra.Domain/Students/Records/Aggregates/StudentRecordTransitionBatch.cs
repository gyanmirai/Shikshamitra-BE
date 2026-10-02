using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.Records;

public sealed class StudentRecordTransitionBatch : CreationAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public string IdempotencyKey { get; private set; } = "";
	public string RequestJson { get; private set; } = "{}";
	public string ExceptionRowsJson { get; private set; } = "[]";
	private StudentRecordTransitionBatch() { }
	public StudentRecordTransitionBatch(Guid id, Guid tenantId, Guid yearId, string key, string requestJson, string exceptionRowsJson) : base(id)
	{
		TenantId = tenantId; AcademicYearId = yearId;
		IdempotencyKey = Check.NotNullOrWhiteSpace(key, nameof(key), 128).Trim();
		RequestJson = Check.NotNullOrWhiteSpace(requestJson, nameof(requestJson), 1000000); ExceptionRowsJson = exceptionRowsJson;
	}
}
