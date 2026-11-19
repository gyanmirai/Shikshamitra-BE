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
 [Theory]
 [InlineData(0, 0, "Upcoming")]
 [InlineData(20, 0, "Upcoming")]
 [InlineData(100, 0, "Paid")]
 [InlineData(100, 100, "InReconciliation")]
 [InlineData(20, 20, "InReconciliation")]
 public void Future_schedule_preserves_payment_and_clearance_status(decimal paid, decimal reconciliation, string expected)
 {
  var today=new DateTime(2026,10,28);
  var due=new DueFee(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),MonthEnum.November,2026,false,100m,today.AddMonths(1),scheduleSource:ScheduleSource.PaymentTerm,demandDate:today.AddDays(5));
  if(paid>0m)due.ApplyPayment(paid);
  MobileFinanceProjection.InstallmentScheduleStatus(due,today,reconciliation).ShouldBe(expected);
 }
 [Fact] public void Fees_reference_summary_partitions_overdue_due_now_and_upcoming_without_losing_paise()
 {
  var today=new DateTime(2026,10,28);var student=Guid.NewGuid();var head=Guid.NewGuid();
  var overdue=new DueFee(Guid.NewGuid(),Guid.NewGuid(),student,head,MonthEnum.October,2026,false,100.10m,today.AddDays(-1));
  overdue.ApplyPayment(20.01m);
  var dueNow=new DueFee(Guid.NewGuid(),Guid.NewGuid(),student,head,MonthEnum.October,2026,false,200.20m,today);
  dueNow.ApplyPayment(50.02m);
  var future=new DueFee(Guid.NewGuid(),Guid.NewGuid(),student,head,MonthEnum.November,2026,false,400.40m,today.AddMonths(1),scheduleSource:ScheduleSource.PaymentTerm,demandDate:today.AddDays(1));
  var overview=MobileFinanceProjection.Overview(student,Guid.NewGuid(),today,[overdue,dueNow,future],[],[],[]);
  var json=System.Text.Json.JsonSerializer.SerializeToElement(overview);
  json.TryGetProperty("DueNowAmount",out var due).ShouldBeTrue("The Fees card requires a server-projected due-now total.");
  due.GetString().ShouldBe("150.18");
  json.GetProperty("RemainingAmount").GetString().ShouldBe("630.67");
  overview.FeeOverview.OverdueFee.ShouldBe("80.09");
  overview.FutureScheduledAmount.ShouldBe("400.40");
 }
 [Fact] public void Overview_keeps_billed_future_and_credits_separate_in_decimal()
 {
  var student = Guid.NewGuid(); var head = Guid.NewGuid(); var today = new DateTime(2026,10,26);
  var billed = new DueFee(Guid.NewGuid(), Guid.NewGuid(),student,head,MonthEnum.October,2026,false,100.10m,today);
  billed.ApplyPayment(20.01m); billed.ApplyConcession(10.02m); billed.ApplyAdjustmentCredit(5.03m);
  var future = new DueFee(Guid.NewGuid(),Guid.NewGuid(),student,head,MonthEnum.November,2026,false,200.20m,today.AddMonths(1),scheduleSource:ScheduleSource.PaymentTerm,demandDate:today.AddDays(1));
  var result = MobileFinanceProjection.Overview(student,Guid.NewGuid(),today,[billed,future],[],[],[]);
  result.FutureScheduledAmount.ShouldBe("200.20");
  result.FeeOverview.PendingFee.ShouldBe("65.04"); result.FeeOverview.ConcessionAmount.ShouldBe("10.02"); result.FeeOverview.AdjustmentCreditAmount.ShouldBe("5.03");
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
  dto.Id.ShouldBe(receipt.Id);dto.Id.ShouldNotBe(transaction);dto.FeeCollectionTransactionId.ShouldBe(transaction);dto.LinkedFinalReceiptId.ShouldBe(receipt.LinkedFinalReceiptId);dto.TotalPaidAmount.ShouldBe("0.30");
 }
 [Fact] public void Future_unbilled_installment_still_exposes_the_next_due_date_and_amount()
 {
  var today=new DateTime(2026,10,26);var dueDate=today.AddMonths(1);
  var due=new DueFee(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),MonthEnum.November,2026,false,123.45m,dueDate,scheduleSource:ScheduleSource.PaymentTerm,demandDate:today.AddDays(5));
  var result=MobileFinanceProjection.Overview(due.StudentId!.Value,Guid.NewGuid(),today,[due],[],[],[]);
  result.FeeOverview.PendingFee.ShouldBe("0.00");result.FutureScheduledAmount.ShouldBe("123.45");
  result.DueNowAmount.ShouldBe("0.00");result.RemainingAmount.ShouldBe("123.45");
  result.NextDueDate.ShouldBe(DateOnly.FromDateTime(dueDate));result.NextDueAmount.ShouldBe("123.45");
 }
 [Fact] public void Empty_fees_reference_summary_has_real_zero_totals()
 {
  var result=MobileFinanceProjection.Overview(Guid.NewGuid(),Guid.NewGuid(),new DateTime(2026,10,28),[],[],[],[]);
  result.DueNowAmount.ShouldBe("0.00");result.RemainingAmount.ShouldBe("0.00");
 }
 [Fact] public void Fully_allocated_charge_is_in_reconciliation_until_instrument_clearance()
 {
  var charge=new MobileChargeBalance(Guid.NewGuid(),"Library charge",null,100m,100m,0m,100m);
  charge.ToDto().PaymentStatus.ShouldBe("InReconciliation");
 }
 [Fact] public void Missing_or_incomplete_decimal_payment_evidence_never_becomes_a_zero_payment()
 {
  Should.Throw<BusinessException>(()=>MobileFinanceProjection.PaymentAmount(100,[])).Code.ShouldBe("Finance:SelfService:PaymentEvidenceIncomplete");
  Should.Throw<BusinessException>(()=>MobileFinanceProjection.PaymentAmount(100,[10m]));
  MobileFinanceProjection.PaymentAmount(0.30,[0.10m,0.20m]).ShouldBe(0.30m);
  MobileFinanceProjection.PaymentAmount(0,[]).ShouldBe(0m);
 }

 [Fact] public void Canonical_fee_overview_is_annual_principal_with_excess_refunds_and_credits_separate()
 {
  var today = new DateTime(2026,10,28); var student = Guid.NewGuid(); var head = Guid.NewGuid();
  var due = new DueFee(Guid.NewGuid(),Guid.NewGuid(),student,head,MonthEnum.October,2026,false,1000m,today.AddDays(-1));
  due.ApplyConcession(100m); due.ApplyAdjustmentCredit(50m); due.ApplyPayment(1200m); due.ApplyRefund(200m);
  var future = new DueFee(Guid.NewGuid(),Guid.NewGuid(),student,head,MonthEnum.November,2026,false,500m,today.AddMonths(1),scheduleSource:ScheduleSource.PaymentTerm,demandDate:today.AddDays(1));
  var overdue = new DueFee(Guid.NewGuid(),Guid.NewGuid(),student,head,MonthEnum.October,2026,false,300m,today.AddDays(-1));
  var overview = MobileFinanceProjection.Overview(student,Guid.NewGuid(),today,[due,future,overdue],
   [new MobileChargeBalance(Guid.NewGuid(),"Charge",null,100m,0m,0m)],[],[new(Guid.NewGuid(),1400m,FeeCollectionStatus.Paid)],75m);
  var fee=overview.FeeOverview;
  fee.TotalFee.ShouldBe("1800.00"); fee.NetFee.ShouldBe("1650.00"); fee.PaidFee.ShouldBe("850.00");
  fee.PendingFee.ShouldBe("300.00"); fee.OverdueFee.ShouldBe("300.00"); fee.ReconciliationAmount.ShouldBe("75.00");
  fee.GrossPaidAmount.ShouldBe("1200.00"); fee.RefundedAmount.ShouldBe("200.00"); fee.OverpaymentAmount.ShouldBe("150.00");
  fee.ConcessionAmount.ShouldBe("100.00"); fee.AdjustmentCreditAmount.ShouldBe("50.00");
  overview.ChargeOutstandingAmount.ShouldBe("100.00");
 }
 [Fact] public void Wire_requires_non_null_fee_overview_with_exact_web_names_and_zero_money_strings()
 {
  var overview=MobileFinanceProjection.Overview(Guid.NewGuid(),Guid.NewGuid(),DateTime.Today,[],[],[],[]);
  var json=System.Text.Json.JsonSerializer.SerializeToElement(overview,new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
  var fee=json.GetProperty("feeOverview");
  var names=new[]{"totalFee","netFee","paidFee","pendingFee","concessionAmount","overdueFee","reconciliationAmount","grossPaidAmount","refundedAmount","overpaymentAmount","adjustmentCreditAmount"};
  System.Linq.Enumerable.Count(fee.EnumerateObject()).ShouldBe(names.Length);
  foreach(var name in names)fee.GetProperty(name).GetString().ShouldBe("0.00");
  json.TryGetProperty("verifiedPaidAmount",out _).ShouldBeFalse();
 }

 [Fact] public void Statement_summary_preserves_future_and_charges_with_web_labels_and_principal_values()
 {
  var overview=MobileFinanceProjection.Overview(Guid.NewGuid(),Guid.NewGuid(),DateTime.Today,[],[],[],[])
   with { FutureScheduledAmount="500.00", ChargeOutstandingAmount="90.00", LateFeeAmount="10.00",
    FeeOverview=new("1500.00","1400.00","300.00","600.00","100.00","200.00","50.00","400.00","20.00","80.00","0.00") };
  var rows=System.Linq.Enumerable.ToDictionary(MobileFinanceProjection.StatementSummary(overview),x=>x.Label,x=>x.Amount);
  rows["Total fee"].ShouldBe("1500.00"); rows["Net fee"].ShouldBe("1400.00"); rows["Paid fee"].ShouldBe("300.00");
  rows["Pending fee"].ShouldBe("600.00"); rows["Overdue fee"].ShouldBe("200.00"); rows["In Reconciliation"].ShouldBe("50.00");
  rows["Concession"].ShouldBe("100.00"); rows["Recorded fee payments"].ShouldBe("400.00"); rows["Refunded"].ShouldBe("20.00");
  rows["Excess fee payment"].ShouldBe("80.00"); rows["Adjustment credits"].ShouldBe("0.00");
  rows["Future installments"].ShouldBe("500.00"); rows["Additional charge outstanding"].ShouldBe("90.00"); rows["Late fee outstanding"].ShouldBe("10.00");
  rows.Count.ShouldBe(15);
 }
}
