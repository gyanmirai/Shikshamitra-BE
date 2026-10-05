using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
namespace ShikshaMitra.Finance.Accounts.Settings;
public record FinanceRoutingOption(Guid Id, string Label);
public record FinanceRoutingOptions(List<FinanceRoutingOption> IncomeCategories,
 List<FinanceRoutingOption> ExpenseCategories, List<FinanceRoutingOption> BankAccounts);
public interface IFinanceRoutingOptionsAppService : IApplicationService
{
 Task<FinanceRoutingOptions> GetAsync(Guid academicYearId, string operation);
}
