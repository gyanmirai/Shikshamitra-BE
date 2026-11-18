using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Accounts.Transactions;
namespace ShikshaMitra.Domain.Finance.Accounts.Settings;
public record FinanceRoutingLookup(Guid Id, string Name);
public interface IFinanceRoutingLookupReader
{
    Task<List<FinanceRoutingLookup>> GetCategoriesAsync(Guid? tenantId, Guid academicYearId, TransactionType categoryType, DateTimeOffset instant);
    Task<List<FinanceRoutingLookup>> GetBanksAsync(Guid? tenantId, Guid academicYearId, DateTimeOffset instant);
}
