using System;
using ShikshaMitra.HR;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.ProfessionalManagement;

public class PerformanceTemplate : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public string Name { get; private set; } = string.Empty;
	public PerformanceStaffCategory StaffCategory { get; private set; }
	public string? RoleName { get; private set; }
	public int RatingScaleMax { get; private set; } = 5;
	public bool AllowNotApplicable { get; private set; } = true;
	public PerformanceTemplateStatus Status { get; private set; }
	public string? Description { get; private set; }

	protected PerformanceTemplate()
	{
	}

	public PerformanceTemplate(Guid id, Guid? tenantId, string name, PerformanceStaffCategory staffCategory, string? roleName, string? description = null) : base(id)
	{
		TenantId = tenantId;
		Update(name, staffCategory, roleName, 5, true, description);
		Status = PerformanceTemplateStatus.Draft;
	}

	public void Update(string name, PerformanceStaffCategory staffCategory, string? roleName, int ratingScaleMax, bool allowNotApplicable, string? description)
	{
		if (Status == PerformanceTemplateStatus.Published)
		{
			throw new BusinessException(HRErrorCodes.PerformanceTemplatePublished);
		}

		Name = name.Trim();
		StaffCategory = staffCategory;
		RoleName = string.IsNullOrWhiteSpace(roleName) ? null : roleName.Trim();
		RatingScaleMax = Math.Clamp(ratingScaleMax, 1, 10);
		AllowNotApplicable = allowNotApplicable;
		Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
	}

	public void Publish(decimal totalWeight, bool hasValidRubric = true)
	{
		if (Math.Round(totalWeight, 2) != 100m)
		{
			throw new BusinessException(HRErrorCodes.PerformanceTemplateWeightsInvalid);
		}

		if (!hasValidRubric)
		{
			throw new BusinessException(HRErrorCodes.PerformanceTemplateRubricInvalid);
		}

		Status = PerformanceTemplateStatus.Published;
	}

	public void Archive()
	{
		Status = PerformanceTemplateStatus.Archived;
	}
}
