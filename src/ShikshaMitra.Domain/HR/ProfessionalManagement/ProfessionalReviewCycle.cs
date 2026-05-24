using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.ProfessionalManagement;

public class ProfessionalReviewCycle : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public string Name { get; private set; } = string.Empty;
	public DateTime PeriodStart { get; private set; }
	public DateTime PeriodEnd { get; private set; }
	public ProfessionalReviewCycleStatus Status { get; private set; }

	protected ProfessionalReviewCycle()
	{
	}

	public ProfessionalReviewCycle(
		Guid id,
		Guid? tenantId,
		string name,
		DateTime periodStart,
		DateTime periodEnd,
		ProfessionalReviewCycleStatus status) : base(id)
	{
		TenantId = tenantId;
		Update(name, periodStart, periodEnd, status);
	}

	public void Update(
		string name,
		DateTime periodStart,
		DateTime periodEnd,
		ProfessionalReviewCycleStatus status)
	{
		Name = name.Trim();
		PeriodStart = periodStart.Date;
		PeriodEnd = periodEnd.Date;
		Status = status;
	}
}
