using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Accounts.Posting;

/// <summary>A school's financial date range. Posting into a closed range is prohibited.</summary>
public class AccountingPeriod : AggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public DateOnly StartsOn { get; private set; }
    public DateOnly EndsOn { get; private set; }
    public bool IsOpen { get; private set; }

    protected AccountingPeriod() { }

    public AccountingPeriod(Guid id, Guid? tenantId, string code,
        DateOnly startsOn, DateOnly endsOn) : base(id)
    {
        if (id == Guid.Empty || string.IsNullOrWhiteSpace(code) || code.Length > 40 ||
            endsOn < startsOn)
            throw new ArgumentException("An accounting period needs a code and valid dates.");
        TenantId = tenantId;
        Code = code.Trim();
        StartsOn = startsOn;
        EndsOn = endsOn;
        IsOpen = true;
    }

    public bool Contains(DateOnly date) => StartsOn <= date && date <= EndsOn;

    public void Close() => IsOpen = false;

    public void Reopen() => IsOpen = true;
}
