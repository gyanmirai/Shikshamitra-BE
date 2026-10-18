using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Reports;

public class HrReportRunPage : Entity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid RunId { get; private set; }
	public int PageNumber { get; private set; }
	public string RowsJson { get; private set; } = "[]";
	public DateTimeOffset ExpiresAt { get; private set; }
	private HrReportRunPage() { }
	public HrReportRunPage(Guid id, Guid? tenantId, Guid runId, int pageNumber, string rowsJson, DateTimeOffset expiresAt) : base(id)
	{
		TenantId = tenantId; RunId = runId; PageNumber = pageNumber; RowsJson = rowsJson; ExpiresAt = expiresAt;
	}
}