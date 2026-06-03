using System;
using ShikshaMitra.HR;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.ProfessionalManagement;

public class PerformanceTemplateCriterion : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid TemplateId { get; private set; }
	public Guid SectionId { get; private set; }
	public string Name { get; private set; } = string.Empty;
	public string? Description { get; private set; }
	public decimal Weight { get; private set; }
	public bool EvidenceRequired { get; private set; }
	public int DisplayOrder { get; private set; }

	protected PerformanceTemplateCriterion()
	{
	}

	public PerformanceTemplateCriterion(Guid id, Guid? tenantId, Guid templateId, Guid sectionId, string name, string? description, decimal weight, bool evidenceRequired, int displayOrder) : base(id)
	{
		TenantId = tenantId;
		TemplateId = templateId;
		SectionId = sectionId;
		Update(name, description, weight, evidenceRequired, displayOrder);
	}

	public void Update(string name, string? description, decimal weight, bool evidenceRequired, int displayOrder)
	{
		Name = name.Trim();
		Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
		Weight = Math.Clamp(weight, 0m, 100m);
		EvidenceRequired = evidenceRequired;
		DisplayOrder = displayOrder;
	}
}
