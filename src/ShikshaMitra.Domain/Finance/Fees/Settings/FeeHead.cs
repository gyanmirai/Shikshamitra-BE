using System;
using System.ComponentModel.DataAnnotations;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Fees.Settings;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Domain.Finance.Fees.Settings;

public class FeeHead : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }

    public Guid AcademicYearId { get; private set; }

    public Guid FeeCategoryId { get; private set; }

    public Guid IncomeCategoryId { get; private set; }

    [Required]
    public string Name { get; private set; }

    public FeeHeadModule? Module { get; private set; }

    public bool IsConcessionApplied { get; private set; }

    public string? Description { get; private set; }

    public bool IsActive { get; private set; }

    public string? DeactivationReason { get; private set; }

	public FeeCategory FeeCategory { get; private set; }

	public TransactionCategory IncomeCategory { get; private set; }

    protected FeeHead() { }

    public FeeHead(
        Guid id,
        Guid? tenantId,
        Guid academicYearId,
        Guid feeCategoryId,
        string name,
        FeeHeadModule? module,
        bool isConcessionApplied,
        string? description,
        Guid incomeCategoryId,
        bool isActive = true) : base(id)
    {
        TenantId = tenantId;
        AcademicYearId = academicYearId;
        FeeCategoryId = feeCategoryId;
        IncomeCategoryId = incomeCategoryId;
        Name = name;
        Module = module;
        IsConcessionApplied = isConcessionApplied;
        Description = description;
        IsActive = isActive;
    }

    public void Update(Guid feeCategoryId, string name, FeeHeadModule? module, bool isConcessionApplied,
        string? description, Guid incomeCategoryId)
    {
        FeeCategoryId = feeCategoryId;
        IncomeCategoryId = incomeCategoryId;
        Name = name;
        Module = module;
        IsConcessionApplied = isConcessionApplied;
        Description = description;
    }

    public void SetActiveStatus(bool isActive, string? deactivationReason)
    {
        IsActive = isActive;
        DeactivationReason = isActive ? null : deactivationReason;
    }
}
