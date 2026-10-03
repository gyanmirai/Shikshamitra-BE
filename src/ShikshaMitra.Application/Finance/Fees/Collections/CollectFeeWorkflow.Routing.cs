using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Transactions;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using ShikshaMitra.Finance.Fees.FeeCollection;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
namespace ShikshaMitra.Finance.Fees.Collections;
public partial class CollectFeeWorkflow
{
 private sealed record RoutingLine(Guid CategoryId, string CategoryName, decimal Amount);
 private sealed record RoutingPlan(List<RoutingLine> Lines, List<(Guid? Id,string? Label)> Banks);
 private async Task<RoutingPlan> PrepareIncomeRoutingAsync(CollectFeeInput input,
   DueFeeValidationContext dues, ChargeValidationContext charges, Dictionary<Guid, DueFee> lateFees)
 {
   var lines = new List<RoutingLine>();
   foreach (var group in input.DueFeePayments.GroupBy(x => x.FeeHeadId).OrderBy(x => x.Key))
   {
     var category = await routingManager.RequireCategoryAsync(input.AcademicYearId,
       dues.FeeHeadMap[group.Key].IncomeCategoryId, TransactionType.Income);
     lines.Add(new(category.Id, category.Name, group.Sum(x => Money(x.Amount))));
   }
   foreach (var payment in input.ChargePayments.OrderBy(x => x.ChargeId))
   {
     var charge = charges.ChargeMap[payment.ChargeId];
     var category = await routingManager.RequireCategoryAsync(input.AcademicYearId,
       charge.IncomeCategoryId, TransactionType.Income);
     lines.Add(new(category.Id, charge.IncomeCategoryName ?? category.Name, Money(payment.Amount)));
   }
   foreach (var payment in input.LateFeePayments.OrderBy(x => x.DueFeeId))
   {
     var due = lateFees[payment.DueFeeId];
     var assignment = due.StudentFeeStructureId.HasValue
       ? await studentFeeStructureRepository.GetAsync(due.StudentFeeStructureId.Value)
       : null;
     if (assignment == null) throw new BusinessException(FinanceRoutingErrorCodes.CategoryRequired);
     var rules = await policyResolver.ResolveEffectiveRulesForStudentAsync(input.StudentId, assignment.FeeStructureStandardId, assignment.Id);
     var category = await routingManager.RequireCategoryAsync(input.AcademicYearId,
       rules.Policy?.LateFeeIncomeCategoryId, TransactionType.Income);
     lines.Add(new(category.Id, category.Name, Money(payment.Amount)));
   }
   var banks = new List<(Guid? Id,string? Label)>();
   foreach (var payment in input.Payments)
   {
     Money(payment.Amount);
     var bank = payment.BankAccountId.HasValue || payment.PaymentMethod != PaymentMethodEnum.Cash
       ? await routingManager.RequireBankAsync(input.AcademicYearId, payment.BankAccountId) : null;
     banks.Add((bank?.Id, bank == null ? null : $"{bank.BankName} • {bank.AccountNumber[^Math.Min(4, bank.AccountNumber.Length)..]}"));
     if (input.ChargePayments.Any(selected => !charges.ChargeMap[selected.ChargeId].ReceivingBankAccountId.HasValue))
       throw new BusinessException(FinanceRoutingErrorCodes.BankRequired);
     if (input.ChargePayments.Any(selected => charges.ChargeMap[selected.ChargeId].ReceivingBankAccountId != bank?.Id))
       throw new UserFriendlyException("Collect charges using their selected receiving bank. Collect charges with different receiving banks separately.");
   }
   if (lines.Sum(x => x.Amount) != input.Payments.Sum(x => Money(x.Amount)))
     throw new BusinessException(FinanceRoutingErrorCodes.AllocationMismatch);
   return new(lines, banks);
 }
 private async Task SaveIncomeRoutingAsync(CollectFeeInput input,
   FeeCollectionManager.CollectFeeTransactionResult transaction, RoutingPlan plan, bool confirmed)
 {
   var allocations = ShikshaMitra.Finance.Accounts.FinanceIncomeAllocator.Allocate(
     plan.Lines.Select(x => x.Amount).ToArray(), input.Payments.Select(x => Money(x.Amount)).ToArray());
   for (var index = 0; index < input.Payments.Count; index++)
   {
     var payment = input.Payments[index]; var bank = plan.Banks[index];
     var record = transaction.PaymentRecords![index]; record.SetReceivingBank(bank.Id, bank.Label);
     foreach (var allocation in allocations.Where(x => x.InstrumentIndex == index))
     {
       var line = plan.Lines[allocation.CategoryIndex];
       await automaticTransactions.RecordAsync(input.AcademicYearId, line.CategoryId, line.CategoryName,
         bank.Id, bank.Label, transaction.ReceiptNumber, allocation.Amount, (int)payment.PaymentMethod,
         TransactionType.Income, $"collection:{transaction.Metadata.Id:N}:{index}:{allocation.CategoryIndex}",
         transaction.Metadata.Id, input.StudentId, confirmed, record.PaymentDate.UtcDateTime);
     }
   }
 }
 private static decimal Money(double value)
 {
   if (!double.IsFinite(value) || value <= 0 || value > 999999999999.99d)
     throw new BusinessException(FinanceRoutingErrorCodes.AllocationMismatch);
   var amount = Convert.ToDecimal(value);
   if (decimal.Round(amount, 2) != amount) throw new UserFriendlyException("Payment amounts must have at most two decimal places.");
   return amount;
 }
}
