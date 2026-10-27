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
using Volo.Abp.Domain.Entities;
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
     if (assignment == null && !input.FinanceFeeAccountId.HasValue)
       throw new BusinessException(FinanceRoutingErrorCodes.CategoryRequired);
     var rules = input.FinanceFeeAccountId.HasValue
       ? await policyResolver.ResolveEffectiveRulesForAccountAsync(input.FinanceFeeAccountId.Value)
       : await policyResolver.ResolveEffectiveRulesForStudentAsync(input.StudentId!.Value, assignment!.FeeStructureStandardId, assignment.Id);
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
     if (payment.PaymentMethod == PaymentMethodEnum.Cash)
     {
       var payer = await CashPayerNameAsync(input);
       await LazyServiceProvider.LazyGetRequiredService<IRepository<ShikshaMitra.Finance.Accounts.CashManagement.CashCollection, Guid>>()
         .InsertAsync(new ShikshaMitra.Finance.Accounts.CashManagement.CashCollection(GuidGenerator.Create(), CurrentTenant.Id,
           input.AcademicYearId, transaction.Metadata.Id, $"collection:{transaction.Metadata.Id:N}:{index}:", null,
           transaction.ReceiptNumber, payer, Money(payment.Amount), record.PaymentDate,
           CurrentUser.Id ?? throw new Volo.Abp.Authorization.AbpAuthorizationException(), input.StudentId, input.FinanceFeeAccountId, record.Id), autoSave: true);
     }
     foreach (var allocation in allocations.Where(x => x.InstrumentIndex == index))
     {
       var line = plan.Lines[allocation.CategoryIndex];
       await automaticTransactions.RecordAsync(input.AcademicYearId, line.CategoryId, line.CategoryName,
         bank.Id, bank.Label, transaction.ReceiptNumber, allocation.Amount, (int)payment.PaymentMethod,
         TransactionType.Income, $"collection:{transaction.Metadata.Id:N}:{index}:{allocation.CategoryIndex}",
         transaction.Metadata.Id, input.StudentId, confirmed && payment.PaymentMethod != PaymentMethodEnum.Cash, record.PaymentDate.UtcDateTime, input.FinanceFeeAccountId);
     }
   }
 }
 private async Task<string> CashPayerNameAsync(CollectFeeInput input)
 {
   // Cash evidence needs a label, not the student's complete related-data graph.
   if (input.FinanceFeeAccountId.HasValue)
   {
     var accounts = await LazyServiceProvider.LazyGetRequiredService<IRepository<FinanceFeeAccount, Guid>>().GetQueryableAsync();
     return await AsyncExecuter.FirstOrDefaultAsync(accounts
       .Where(x => x.Id == input.FinanceFeeAccountId.Value && x.TenantId == CurrentTenant.Id)
       .Select(x => x.ApplicantName))
       ?? throw new EntityNotFoundException(typeof(FinanceFeeAccount), input.FinanceFeeAccountId.Value);
   }
   var students = await LazyServiceProvider.LazyGetRequiredService<ShikshaMitra.Students.IStudentRepository>()
     .GetQueryableWithoutAutoIncludesAsync();
   var student = await AsyncExecuter.FirstOrDefaultAsync(students
     .Where(x => x.Id == input.StudentId!.Value && x.TenantId == CurrentTenant.Id)
     .Select(x => new { x.FirstName, x.LastName }))
     ?? throw new EntityNotFoundException(typeof(ShikshaMitra.Students.Student), input.StudentId!.Value);
   return string.Join(" ", new[] { student.FirstName, student.LastName }.Where(x => !string.IsNullOrWhiteSpace(x)));
 }
 private static decimal Money(double value)
 {
   if (!double.IsFinite(value) || value <= 0 || value > 999999999999.99d)
     throw new BusinessException(FinanceRoutingErrorCodes.AllocationMismatch);
   var amount = Convert.ToDecimal(value);
   if (decimal.Round(amount, 2) != amount) throw new BusinessException(FinanceRoutingErrorCodes.AllocationMismatch);
   return amount;
 }
}
