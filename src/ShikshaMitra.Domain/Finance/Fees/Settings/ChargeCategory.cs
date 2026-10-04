using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Data;

namespace ShikshaMitra.Domain.Finance.Fees.Settings;

public class ChargeCategory : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	private const string IncomeCategoryIdProperty = "ChargeIncomeCategoryId";
	private const string IncomeAccountHeadIdProperty = "ChargeIncomeAccountHeadId";
	private const string StatusReasonProperty = "ChargeCategoryStatusReason";
	private const string StatusChangedByProperty = "ChargeCategoryStatusChangedBy";
	private const string StatusChangedOnProperty = "ChargeCategoryStatusChangedOn";
    public Guid? TenantId { get; private set; }
    public Guid AcademicYearId { get; set; }
    public string Name { get; set; }
    public bool IsApprovalRequired { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
	public Guid? IncomeCategoryId => this.GetProperty<Guid?>(IncomeCategoryIdProperty);
	public Guid? IncomeAccountHeadId => this.GetProperty<Guid?>(IncomeAccountHeadIdProperty);
	public bool IsAccountingReady => IncomeCategoryId.HasValue;

    protected ChargeCategory() { }

    public ChargeCategory(
        Guid id,
        Guid? tenantId,
        Guid academicYearId,
        string name,
        bool isApprovalRequired,
        string? description,
        bool isActive = true)
        : base(id)
    {
        TenantId = tenantId;
        AcademicYearId = academicYearId;
        Name = name;
        IsApprovalRequired = isApprovalRequired;
        Description = description;
        IsActive = isActive;
    }

	public void SetAccountingMapping(Guid incomeCategoryId, Guid? incomeAccountHeadId = null)
	{
		this.SetProperty(IncomeCategoryIdProperty, incomeCategoryId);
		this.SetProperty(IncomeAccountHeadIdProperty, incomeAccountHeadId);
	}

	public void SetStatus(bool isActive, Guid? changedBy, string? reason)
	{
		IsActive = isActive;
		this.SetProperty(StatusReasonProperty, reason?.Trim());
		this.SetProperty(StatusChangedByProperty, changedBy);
		this.SetProperty(StatusChangedOnProperty, DateTimeOffset.UtcNow);
	}
}
