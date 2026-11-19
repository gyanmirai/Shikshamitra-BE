using System;
using System.Linq;
using System.Threading.Tasks;

namespace ShikshaMitra.Finance.Accounts.Activity;

/// <summary>Provider-specific scalar evidence, composed with the owning service's authorized queries.</summary>
public interface IAccountsRegisterEvidenceQuery
{
    Task<IQueryable<AccountsTransactionParty>> PartiesAsync(Guid? tenantId, Guid academicYearId);
    Task<IQueryable<AccountsRefundEntryTime>?> RefundTimesAsync(Guid? tenantId, Guid academicYearId);
}

public sealed class AccountsTransactionParty
{
    public Guid Id { get; set; }
    public string Counterparty { get; set; } = "";
}

public sealed class AccountsRefundEntryTime
{
    public Guid Id { get; set; }
    public DateTime PostedAtUtc { get; set; }
}
