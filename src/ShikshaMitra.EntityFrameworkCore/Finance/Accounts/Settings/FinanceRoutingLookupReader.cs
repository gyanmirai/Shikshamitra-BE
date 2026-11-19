using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Finance.Accounts.Transactions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
namespace ShikshaMitra.Finance.Accounts.Settings;
public class FinanceRoutingLookupReader(
    IRepository<TransactionCategory, Guid> categories, IRepository<BankAccount, Guid> banks,
    IDbContextProvider<ShikshaMitraDbContext> contextProvider) : IFinanceRoutingLookupReader, ITransientDependency
{
    public async Task<List<FinanceRoutingLookup>> GetCategoriesAsync(Guid? tenantId, Guid academicYearId, TransactionType categoryType, DateTimeOffset instant)
    {
        var sqlite = (await contextProvider.GetDbContextAsync()).Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite";
        var query = (await categories.GetQueryableAsync()).AsNoTracking().Where(x => x.TenantId == tenantId && x.AcademicYearId == academicYearId && x.IsActive && x.CategoryType == categoryType);
        if (!sqlite) query = query.Where(x => (!x.EffectiveFrom.HasValue || x.EffectiveFrom <= instant) && (!x.EffectiveTo.HasValue || x.EffectiveTo >= instant));
        var rows = await query.OrderBy(x => x.Name.Trim().ToUpper() == "NONE" ? 0 : 1).ThenBy(x => x.Name).ThenBy(x => x.Id)
            .Select(x => new { x.Id, x.Name, x.EffectiveFrom, x.EffectiveTo }).ToListAsync();
        return rows.Where(x => !sqlite || (!(x.EffectiveFrom > instant) && !(x.EffectiveTo < instant))).Select(x => new FinanceRoutingLookup(x.Id, x.Name)).ToList();
    }
    public async Task<List<FinanceRoutingLookup>> GetBanksAsync(Guid? tenantId, Guid academicYearId, DateTimeOffset instant)
    {
        var sqlite = (await contextProvider.GetDbContextAsync()).Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite";
        var query = (await banks.GetQueryableAsync()).AsNoTracking().Where(x => x.TenantId == tenantId && x.AcademicYearId == academicYearId && x.IsActive);
        if (!sqlite) query = query.Where(x => (!x.EffectiveFrom.HasValue || x.EffectiveFrom <= instant) && (!x.EffectiveTo.HasValue || x.EffectiveTo >= instant));
        var rows = await query.OrderBy(x => x.BankName).ThenBy(x => x.Id).Select(x => new { x.Id, x.BankName,
            LastFour = x.AccountNumber.Length <= 4 ? x.AccountNumber : x.AccountNumber.Substring(x.AccountNumber.Length - 4), x.EffectiveFrom, x.EffectiveTo }).ToListAsync();
        return rows.Where(x => !sqlite || (!(x.EffectiveFrom > instant) && !(x.EffectiveTo < instant))).Select(x => new FinanceRoutingLookup(x.Id, $"{x.BankName} • {x.LastFour}")).ToList();
    }
}
