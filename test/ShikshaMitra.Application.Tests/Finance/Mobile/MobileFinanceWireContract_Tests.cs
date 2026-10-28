using System;
using System.IO;
using System.Text.Json;
using System.Linq;
using ShikshaMitra.Finance.Fees.FeeRefund.Dtos;
using ShikshaMitra.Finance.Fees.Concessions.Dtos;
using ShikshaMitra.Finance.Mobile;
using Shouldly;
using Xunit;
namespace ShikshaMitra.Finance.Mobile;
public class MobileFinanceWireContract_Tests
{
 [Fact] public void Read_contracts_use_canonical_web_names()
 {
  typeof(MobileFinanceInstallment).GetProperties().Select(x=>x.Name).ShouldContain("DueFeeId");
  typeof(MobileFinancePayment).GetProperties().Select(x=>x.Name).ShouldContain("PaymentId");
  typeof(MobileFamilyCharge).GetProperties().Select(x=>x.Name).ShouldContain("ChargeLineId");
  typeof(MobileFinanceOverview).GetProperties().Select(x=>x.Name).ShouldNotContain("BilledAmount");
  var chargePaging=typeof(IMobileFinanceReadService).GetMethod("ChargePaymentsAsync")!.GetParameters();
  chargePaging[3].Name.ShouldBe("page");chargePaging[3].DefaultValue.ShouldBe(1);
  var historyPaging=typeof(IMobileFinanceReadService).GetMethod("PaymentsAsync")!.GetParameters();
  historyPaging[2].Name.ShouldBe("skipCount");historyPaging[2].DefaultValue.ShouldBe(0);
 }

 public static JsonElement Write(string name,object value)
 {
  var options=new JsonSerializerOptions(JsonSerializerDefaults.Web){WriteIndented=true};
  var json=JsonSerializer.Serialize(new {success=true,data=value},options);
  var output=Environment.GetEnvironmentVariable("FAMILY_CHARGES_WIRE_OUTPUT");
  if(!string.IsNullOrWhiteSpace(output)){Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,name),json);}
  return JsonDocument.Parse(json).RootElement.GetProperty("data").Clone();
 }
 [Fact] public void Runtime_serializer_exports_canonical_all_module_fixtures_without_generic_aliases()
 {
  var id=Guid.Parse("10000000-0000-0000-0000-000000000001");var year=Guid.Parse("20000000-0000-0000-0000-000000000001");
  var receipt=new MobileFinanceReceipt(id,"R-001","FinalFee","Fee","Verified","1000.00",new(2026,10,28),new(2026,10,28,10,0,0,DateTimeKind.Utc),id,true,null,null,null,null);
  var payment=new MobileFinancePayment(id,"R-001",new(2026,10,28),"1000.00","Paid",["Cash"],[MobileFinanceProjection.ReceiptReference(receipt)]);
  var installment=new MobileFinanceInstallment(id,null,"October 2026",id,"Tuition",new(2026,10,28),null,"1000.00","300.00","600.00","100.00","100.00","0.00","Partial");
  var installmentJson=Write("installments.json",new[]{installment});
  installmentJson[0].GetProperty("dueFeeId").GetGuid().ShouldBe(id);installmentJson[0].GetProperty("paid").GetString().ShouldBe("300.00");
  var paymentJson=Write("payments.json",new MobileFinanceList<MobileFinancePayment>([payment],1) { PaymentSummary=new(id,year,"1000.00",1,new(2026,10,28)) });
  paymentJson.GetProperty("paymentSummary").GetProperty("paidAmount").GetString().ShouldBe("1000.00");
  paymentJson.TryGetProperty("page",out _).ShouldBeFalse();paymentJson.GetProperty("items")[0].GetProperty("receipts")[0].TryGetProperty("id",out _).ShouldBeFalse();
  var receiptJson=Write("receipts.json",new MobileFinanceList<MobileFinanceReceipt>([receipt],1));
  receiptJson.GetProperty("items")[0].GetProperty("totalPaidAmount").GetString().ShouldBe("1000.00");
  foreach(var alias in new[]{"kind","date","amount","paymentId","linkedReceiptId"})receiptJson.GetProperty("items")[0].TryGetProperty(alias,out _).ShouldBeFalse();
  var concession=new MobileConcessionRequest(id,id,year,"Sibling","200.00","200.00","150.00",DateTimeOffset.Parse("2026-10-28T10:00:00Z"),"Approved","Approved","stamp",[],null);
  var refund=new MobileRefundRequest(id,"RV-001","1000.00","500.00","500.00","500.00","100.00",DateTimeOffset.Parse("2026-10-28T10:00:00Z"),new(2026,10,28),"Processed",[],"Creator only",id,"R-001");
  var adjustment=new MobileAdjustmentRequest(id,"Pending","75.00",DateTimeOffset.Parse("2026-10-28T10:00:00Z"),"Creator adjustment",[]);
  Write("adjustments.json",new MobileFinanceRequestPage([adjustment],1,null,null,null));
  var requests=Write("requests.json",new MobileFinanceRequestPage([concession,refund,adjustment],3,null,null,null));
  requests.GetProperty("items")[0].GetProperty("requestedConcessionAmount").GetString().ShouldBe("200.00");
  foreach(var row in requests.GetProperty("items").EnumerateArray())foreach(var alias in new[]{"title","amount","date"})row.TryGetProperty(alias,out _).ShouldBeFalse();
  var activity=new MobileFinancePage<MobileFinanceActivityItem>([new(id,"refund","Refund request","Submitted",new(2026,10,28,10,0,0,DateTimeKind.Utc),false)],21,2,20);
  var activityJson=Write("activity.json",activity);Write("notifications.json",activity);
  activityJson.GetProperty("page").GetInt32().ShouldBe(2);activityJson.TryGetProperty("currentPage",out _).ShouldBeFalse();
  Write("concessions.json",new MobileFinanceRequestPage([concession],1,1,20,1) { RequestSummary=new(id,year,"concession","150.00",0) });
  Write("refunds.json",new MobileFinanceRequestPage([refund],1,null,null,null) { RequestSummary=new(id,year,"refund","400.00",0) });
  Write("refund-result.json",refund);
  var eligibility=MobileFinanceCanonicalCommandProjection.Map(new RefundEligibilityDto{StudentId=id,AcademicYearId=year,RefundPolicyName="Half refund",AllowPartialRefund=true,
   AllowedPayoutMethods=["Cash"],TotalPaid=1000m,TotalRefundable=500m,Receipts=[new(){ReceiptId=id,ReceiptNumber="R-001",ReceiptDate=new(2026,10,28),PaidAmount=1000m,RefundableAmount=500m}],
   FeeHeads=[new(){FeeHeadId=id,FeeHeadName="Tuition",TotalPaid=1000m,TotalRefundable=500m,Months=[new(){DueFeeId=id,SourceAllocationId=id,SourceReceiptId=id,ReceiptNumber="R-001",Month=ShikshaMitra.Finance.Fees.Collections.MonthEnum.October,Year=2026,Total=1000m,Paid=1000m,Refundable=500m}]}]});
  var eligibleJson=Write("refund-eligibility.json",eligibility);
  eligibleJson.GetProperty("feeHeads")[0].GetProperty("months")[0].GetProperty("refundable").GetString().ShouldBe("500.00");
  var requirement=MobileFinanceCanonicalCommandProjection.Map(new ConcessionDocumentRequirementDto {DocumentName="Evidence",AcceptedFormats=[ShikshaMitra.Common.DocumentType.Pdf]});
  requirement.AcceptedFormats.ShouldBe(new[]{"Pdf"});
  var submit=Write("concession-submit-input.json",new SubmitConcessionRequestDto {IdempotencyKey="retry-submit",ConcurrencyStamp="stamp",PreviewFingerprint="fingerprint"});
  submit.TryGetProperty("requestConcurrencyStamp",out _).ShouldBeFalse();
  var cancel=Write("concession-cancel-input.json",new CancelConcessionRequestDto{IdempotencyKey="retry-cancel",RequestConcurrencyStamp="stamp",Reason="Family cancellation"});
  cancel.TryGetProperty("concurrencyStamp",out _).ShouldBeFalse();
  Write("refund-preview.json",new MobileFeeRefundPreview{StudentFeeStructureId=id,RefundPolicyId=id,RefundPolicyVersionId=id,RefundPolicyName="Half refund",SettledPaidAmount="1000.00",RequestedAmount="1000.00",MaximumAllowedAmount="500.00",IsAllowed=true,PreviewFingerprint="synthetic-fingerprint"});
 }
}
