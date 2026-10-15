using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Accounts.Incomes;
using ShikshaMitra.Finance.Accounts.Transactions;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
namespace ShikshaMitra.Finance.Accounts;
/// <summary>Source-owned operational income/expense, never a journal posting.</summary>
public class AutomaticTransactionManager(IRepository<Transaction, Guid> entries,
 ICurrentTenant tenant, IClock clock) : DomainService
{
 public async Task RecordAsync(Guid yearId, Guid categoryId, string categoryName, Guid? bankId,
   string? bankLabel, string name, decimal amount, int method, TransactionType type,
   string key, Guid sourceId, Guid? studentId, bool confirmed = true, DateTime? paymentDate = null,
   Guid? financeFeeAccountId = null)
 {
   if (await entries.AnyAsync(x => x.SourceKey == key)) return;
   await entries.InsertAsync(Transaction.Automatic(Guid.NewGuid(), tenant.Id, yearId, categoryId,
     categoryName, bankId, bankLabel, name, amount, method, paymentDate ?? clock.Now, type, key, sourceId, studentId, confirmed,
     financeFeeAccountId: financeFeeAccountId), autoSave: true);
 }
 public async Task ConfirmAsync(Guid sourceId)
 {
   var rows = await entries.GetListAsync(x => x.SourceId == sourceId && !x.IsConfirmed);
   foreach (var row in rows) row.Confirm();
 }
 public async Task ReverseAsync(Guid sourceId)
 {
   var rows = await entries.GetListAsync(x => x.SourceId == sourceId && x.Amount > 0 && x.IsConfirmed);
   foreach (var row in rows)
   {
     var key = row.SourceKey + ":return";
     if (await entries.AnyAsync(x => x.SourceKey == key)) continue;
     await entries.InsertAsync(Transaction.Automatic(Guid.NewGuid(), tenant.Id, row.AcademicYearId,
       row.CategoryId, row.CategoryNameSnapshot!, row.BankAccountId, row.BankLabelSnapshot,
       row.Name + " (returned)", row.Amount, row.PaymentMethod, clock.Now, row.TransactionType,
       key, sourceId, row.StudentId, correction:true, financeFeeAccountId: row.FinanceFeeAccountId), autoSave: true);
   }
 }
}
