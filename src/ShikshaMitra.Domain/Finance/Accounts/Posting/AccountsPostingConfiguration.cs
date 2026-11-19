using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Accounts.Posting;

/// <summary>Tenant/year activation gate. New schools start with posting disabled.</summary>
public class AccountsPostingConfiguration : AggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public Guid AcademicYearId { get; private set; }
    public DateOnly? FirstPostingDate { get; private set; }
    public bool IsEnabled { get; private set; }
    public Guid? CashAccountHeadId { get; private set; }

    protected AccountsPostingConfiguration() { }

    public AccountsPostingConfiguration(Guid id, Guid? tenantId, Guid academicYearId) : base(id)
    {
        if (id == Guid.Empty || academicYearId == Guid.Empty)
            throw new ArgumentException("Posting configuration needs an academic year.");
        TenantId = tenantId;
        AcademicYearId = academicYearId;
        IsEnabled = false;
    }

    public void Configure(DateOnly firstPostingDate, Guid cashAccountHeadId)
    {
        if (cashAccountHeadId == Guid.Empty)
            throw new ArgumentException("A school cash account is required.");
        FirstPostingDate = firstPostingDate;
        CashAccountHeadId = cashAccountHeadId;
        IsEnabled = false; // Any mapping change requires a fresh readiness check.
    }

    public void Enable()
    {
        if (!FirstPostingDate.HasValue || !CashAccountHeadId.HasValue)
            throw new InvalidOperationException("Posting configuration is incomplete.");
        IsEnabled = true;
    }

    public void Disable() => IsEnabled = false;
}
