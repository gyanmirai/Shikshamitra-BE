using System;
using ShikshaMitra.Finance.Mobile;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Finance.Fees.Settings.PaymentTerms;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp;
using Xunit;
namespace ShikshaMitra.Finance;
public class MobileFinanceProjection_Tests
{
 [Fact] public void Overview_keeps_billed_future_and_credits_separate_in_decimal()
 {
  var student = Guid.NewGuid(); var head = Guid.NewGuid(); var today = new DateTime(2026,10,26);
  var billed = new DueFee(Guid.NewGuid(), Guid.NewGuid(),student,head,MonthEnum.October,2026,false,100.10m,today);
  billed.ApplyPayment(20.01m); billed.ApplyConcession(10.02m); billed.ApplyAdjustmentCredit(5.03m);
  var future = new DueFee(Guid.NewGuid(),Guid.NewGuid(),student,head,MonthEnum.November,2026,false,200.20m,today.AddMonths(1),scheduleSource:ScheduleSource.PaymentTerm,demandDate:today.AddDays(1));
  var result = MobileFinanceProjection.Overview(student,Guid.NewGuid(),today,[billed,future],[],[],[]);
  result.BilledAmount.ShouldBe("100.10"); result.FutureScheduledAmount.ShouldBe("200.20");
  result.OutstandingAmount.ShouldBe("65.04"); result.ConcessionAmount.ShouldBe("10.02"); result.AdjustmentAmount.ShouldBe("5.03");
 }
 [Fact] public void Receipt_guard_rejects_other_child_year_or_tenant_and_accepts_owned_receipt()
 {
  var student=Guid.NewGuid();var year=Guid.NewGuid();var tenant=Guid.NewGuid();
  var receipt=new FeePaymentReceipt(Guid.NewGuid()){StudentId=student,AcademicYearId=year,TenantId=tenant};
  MobileFinanceProjection.RequireReceiptOwnership(receipt,student,year,tenant);
  Should.Throw<AbpAuthorizationException>(()=>MobileFinanceProjection.RequireReceiptOwnership(receipt,Guid.NewGuid(),year,tenant));
  Should.Throw<AbpAuthorizationException>(()=>MobileFinanceProjection.RequireReceiptOwnership(receipt,student,Guid.NewGuid(),tenant));
  Should.Throw<AbpAuthorizationException>(()=>MobileFinanceProjection.RequireReceiptOwnership(receipt,student,year,Guid.NewGuid()));
 }
 [Fact] public void Student_self_is_read_only_even_with_a_guardian_link()
 {
  MobileFinanceAccessService.IsGuardianOnlyAssociation(true,true).ShouldBeFalse();
  MobileFinanceAccessService.IsGuardianOnlyAssociation(false,true).ShouldBeTrue();
  MobileFinanceAccessService.IsGuardianOnlyAssociation(false,false).ShouldBeFalse();
 }
 [Fact] public void Receipt_projection_preserves_real_receipt_ids_and_kachi_final_linkage()
 {
  var transaction=Guid.NewGuid();var receipt=new FeePaymentReceipt(Guid.NewGuid()){FeeCollectionTransactionId=transaction,LinkedFinalReceiptId=Guid.NewGuid(),TotalPaidAmount=0.30};
  var dto=MobileFinanceProjection.Receipt(receipt);
  dto.Id.ShouldBe(receipt.Id);dto.Id.ShouldNotBe(transaction);dto.PaymentId.ShouldBe(transaction);dto.LinkedReceiptId.ShouldBe(receipt.LinkedFinalReceiptId);dto.Amount.ShouldBe("0.30");
 }
 [Fact] public void Future_unbilled_installment_still_exposes_the_next_due_date_and_amount()
 {
  var today=new DateTime(2026,10,26);var dueDate=today.AddMonths(1);
  var due=new DueFee(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),MonthEnum.November,2026,false,123.45m,dueDate,scheduleSource:ScheduleSource.PaymentTerm,demandDate:today.AddDays(5));
  var result=MobileFinanceProjection.Overview(due.StudentId!.Value,Guid.NewGuid(),today,[due],[],[],[]);
  result.BilledAmount.ShouldBe("0.00");result.FutureScheduledAmount.ShouldBe("123.45");
  result.NextDueDate.ShouldBe(DateOnly.FromDateTime(dueDate));result.NextDueAmount.ShouldBe("123.45");
 }
 [Fact] public void Fully_allocated_charge_is_in_reconciliation_until_instrument_clearance()
 {
  var charge=new MobileChargeBalance(Guid.NewGuid(),"Library charge",null,100m,100m,0m,100m);
  charge.ToDto().Status.ShouldBe("InReconciliation");
 }
 [Fact] public void Missing_or_incomplete_decimal_payment_evidence_never_becomes_a_zero_payment()
 {
  Should.Throw<BusinessException>(()=>MobileFinanceProjection.PaymentAmount(100,[])).Code.ShouldBe("Finance:SelfService:PaymentEvidenceIncomplete");
  Should.Throw<BusinessException>(()=>MobileFinanceProjection.PaymentAmount(100,[10m]));
  MobileFinanceProjection.PaymentAmount(0.30,[0.10m,0.20m]).ShouldBe(0.30m);
  MobileFinanceProjection.PaymentAmount(0,[]).ShouldBe(0m);
 }
}
