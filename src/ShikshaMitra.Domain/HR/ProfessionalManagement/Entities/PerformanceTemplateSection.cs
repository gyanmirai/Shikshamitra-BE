using System;
using ShikshaMitra.HR;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.ProfessionalManagement;

public class PerformanceTemplateSection : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid TemplateId { get; private set; }
	public string Name { get; private set; } = string.Empty;
	public decimal Weight { get; private set; }
	public int DisplayOrder { get; private set; }

	protected PerformanceTemplateSection()
	{
	}

	public PerformanceTemplateSection(Guid id, Guid? tenantId, Guid templateId, string name, decimal weight, int displayOrder) : base(id)
	{
		TenantId = tenantId;
		TemplateId = templateId;
		Update(name, weight, displayOrder);
	}

	public void Update(string name, decimal weight, int displayOrder)
	{
		Name = name.Trim();
		Weight = Math.Clamp(weight, 0m, 100m);
		DisplayOrder = displayOrder;
	}
}
