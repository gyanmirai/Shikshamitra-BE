using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Transactions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
namespace ShikshaMitra.Finance.Accounts.Settings;
[Authorize, RemoteService(false)]
public class FinanceRoutingOptionsAppService(IRepository<TransactionCategory, Guid> categories,
 IRepository<BankAccount, Guid> banks, FinanceScopeAccessService scope) : ApplicationService, IFinanceRoutingOptionsAppService
{
 public async Task<FinanceRoutingOptions> GetAsync(Guid academicYearId, string operation)
 {
   string[] required = operation switch
   {
     "income-entry" => [ShikshaMitraSubmodulePermissions.Finance.Accounts_IncomeTracking.Add],
     "expense-entry" => [ShikshaMitraSubmodulePermissions.Finance.Accounts_ExpenseTracking.Add],
     "collection" => [ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.Add],
     "refund" => [ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Approve, ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Process],
     "refund-policy" => [ShikshaMitraSubmodulePermissions.Finance.Policies_RefundPolicy.Add],
     "collection-policy" => [ShikshaMitraSubmodulePermissions.Finance.Policies_CollectionPolicy.Add],
     "fee-head" => [ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeHead.Add, ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeCategory.Add],
     "charge-category" => [ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_ChargeCategory.Add, ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_ChargeCategory.Edit],
     _ => throw new AbpAuthorizationException()
   };
   var granted = false;
   foreach (var permission in required) if (await AuthorizationService.IsGrantedAsync(permission)) granted = true;
   if (!granted) throw new AbpAuthorizationException();
   if (operation is "collection" or "refund") await scope.EnsureCollectionYearAccessibleAsync(academicYearId);
   else await scope.EnsureAcademicContextAccessibleAsync(academicYearId, null, null, null, null);
   var instant = new DateTimeOffset(DateTime.SpecifyKind(Clock.Now, DateTimeKind.Utc));
   var categoryRows = await categories.GetListAsync(x => x.TenantId == CurrentTenant.Id &&
     x.AcademicYearId == academicYearId && x.IsActive);
   var bankRows = await banks.GetListAsync(x => x.TenantId == CurrentTenant.Id &&
     x.AcademicYearId == academicYearId && x.IsActive);
   // Evaluate DateTimeOffset windows after tenant/year filtering for provider parity.
   categoryRows.RemoveAll(x => x.EffectiveFrom > instant || x.EffectiveTo < instant);
   bankRows.RemoveAll(x => x.EffectiveFrom > instant || x.EffectiveTo < instant);
   var income = operation is "income-entry" or "collection" or "collection-policy" or "fee-head" or "charge-category";
   var expense = operation is "expense-entry" or "refund" or "refund-policy";
   return new(income ? categoryRows.Where(x => x.CategoryType == TransactionType.Income).OrderBy(x=>x.Name).Select(x=>new FinanceRoutingOption(x.Id,x.Name)).ToList() : [],
     expense ? categoryRows.Where(x => x.CategoryType == TransactionType.Expense).OrderBy(x=>x.Name).Select(x=>new FinanceRoutingOption(x.Id,x.Name)).ToList() : [],
     operation is "collection-policy" or "fee-head" or "charge-category" ? [] : bankRows.OrderBy(x=>x.BankName).Select(x=>new FinanceRoutingOption(x.Id,$"{x.BankName} • {x.AccountNumber[^Math.Min(4,x.AccountNumber.Length)..]}")).ToList());
 }
}
