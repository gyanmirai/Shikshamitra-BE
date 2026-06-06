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
	public string? GujaratiName { get; private set; }
	public PerformanceSqaaDomain SqaaDomain { get; private set; } = PerformanceSqaaDomain.HumanResources;
	public decimal Weight { get; private set; }
	public int DisplayOrder { get; private set; }

	protected PerformanceTemplateSection()
	{
	}

	public PerformanceTemplateSection(Guid id, Guid? tenantId, Guid templateId, string name, decimal weight, int displayOrder, string? gujaratiName = null, PerformanceSqaaDomain sqaaDomain = PerformanceSqaaDomain.HumanResources) : base(id)
	{
		TenantId = tenantId;
		TemplateId = templateId;
		Update(name, weight, displayOrder, gujaratiName, sqaaDomain);
	}

	public void Update(string name, decimal weight, int displayOrder, string? gujaratiName = null, PerformanceSqaaDomain sqaaDomain = PerformanceSqaaDomain.HumanResources)
	{
		Name = name.Trim();
		GujaratiName = string.IsNullOrWhiteSpace(gujaratiName) ? null : gujaratiName.Trim();
		SqaaDomain = sqaaDomain;
		Weight = Math.Clamp(weight, 0m, 100m);
		DisplayOrder = displayOrder;
	}
}
