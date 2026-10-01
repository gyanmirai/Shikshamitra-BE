using System;
using ShikshaMitra.Finance.Fees.Collections;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Accounts.Posting;

/// <summary>An approved school-side Asset destination for a payment method.</summary>
public class PaymentDestinationRule : AggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public Guid AcademicYearId { get; private set; }
    public PaymentMethodEnum Method { get; private set; }
    public PaymentDirection Direction { get; private set; }
    public Guid AssetAccountHeadId { get; private set; }
    public Guid? BankAccountId { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }
    public DateOnly? EffectiveTo { get; private set; }
    public bool IsActive { get; private set; }

    protected PaymentDestinationRule() { }

    public PaymentDestinationRule(Guid id, Guid? tenantId, Guid academicYearId,
        PaymentMethodEnum method, PaymentDirection direction, Guid assetAccountHeadId,
        Guid? bankAccountId, DateOnly effectiveFrom, DateOnly? effectiveTo) : base(id)
    {
        if (id == Guid.Empty || academicYearId == Guid.Empty || assetAccountHeadId == Guid.Empty ||
            !Enum.IsDefined(method) || !Enum.IsDefined(direction) ||
            (effectiveTo.HasValue && effectiveTo.Value < effectiveFrom))
            throw new ArgumentException("A payment destination needs valid method, account, and dates.");
        TenantId = tenantId;
        AcademicYearId = academicYearId;
        Method = method;
        Direction = direction;
        AssetAccountHeadId = assetAccountHeadId;
        BankAccountId = bankAccountId;
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
        IsActive = true;
    }

    public bool AppliesOn(DateOnly date) => EffectiveFrom <= date &&
        (!EffectiveTo.HasValue || date <= EffectiveTo.Value);

    public void EndOn(DateOnly lastEffectiveDate)
    {
        if (lastEffectiveDate < EffectiveFrom ||
            (EffectiveTo.HasValue && lastEffectiveDate > EffectiveTo.Value))
            throw new ArgumentException("The last effective date must be inside this rule's date range.");
        EffectiveTo = lastEffectiveDate;
        IsActive = false;
    }
}
