using System;
using System.ComponentModel.DataAnnotations;
using ShikshaMitra.Finance.Fees.Settings;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Domain.Finance.Fees.Settings;

public class FeeCategory : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }

    public Guid AcademicYearId { get; private set; }

    [Required]
    public string Name { get; private set; }

    [Required]
    public string NormalizedName { get; private set; }

    public string? Description { get; private set; }

    public bool IsActive { get; private set; } = true;

    public string? DeactivationReason { get; private set; }

    protected FeeCategory() { }

    public FeeCategory(Guid id, Guid? tenantId, Guid academicYearId, string name, string? description) : base(id)
    {
        TenantId = tenantId;
        AcademicYearId = academicYearId;
        SetName(name);
        Description = description;
        IsActive = true;
    }

    public void Update(string name, string? description)
    {
        SetName(name);
        Description = description;
    }

    public void SetStatus(bool isActive, string? deactivationReason)
    {
		if (!isActive && string.IsNullOrWhiteSpace(deactivationReason))
			throw new Volo.Abp.BusinessException(FinanceFeeCategoryErrorCodes.DeactivationReasonRequired)
				.WithData("Field", "DeactivationReason");
        IsActive = isActive;
		DeactivationReason = isActive ? null : deactivationReason!.Trim();
    }

    private void SetName(string name)
    {
        Name = FeeSetupNameNormalizer.ForStorage(name);
        NormalizedName = FeeSetupNameNormalizer.ForComparison(Name);
    }
}
