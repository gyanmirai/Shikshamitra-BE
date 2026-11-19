using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Accounts.Transactions;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.FeeRefund;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Finance.Mobile;
using Shouldly;
using Volo.Abp;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using Xunit;
using Permissions = ShikshaMitra.Finance.Mobile.MobileFinancePermissions;

namespace ShikshaMitra.Academic.Mobile;

public partial class MobileAcademicAppService_Tests
{
	[Theory]
	[InlineData(RefundAmountMode.ManualUpToEligible, "150.00", "150.00", "student")]
	[InlineData(RefundAmountMode.RemainingPaidAmount, "1200.00", "1200.00", "student")]
	[InlineData(RefundAmountMode.Percentage, "1200.00", "600.00", "student")]
	[InlineData(RefundAmountMode.FixedAmount, "1200.00", "100.00", "student")]
	[InlineData(RefundAmountMode.ManualUpToEligible, "150.00", "150.00", "account")]
	[InlineData(RefundAmountMode.ManualUpToEligible, "150.00", "150.00", "adopted")]
	public Task Family_refund_uses_pinned_rules_and_combined_payment_sources(RefundAmountMode mode, string requested, string expected, string payerKind) => WithUnitOfWorkAsync(new AbpUnitOfWorkOptions { IsTransactional = false }, async () =>
	{
		var tenantId = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var fixture = await FinanceRequestAsync(async () =>
		{
		var f = await Seed(tenantId, true); using var seedIdentity = FinanceIdentity(f.User.Id);
		await GrantFinancePermissionAsync(f.User.Id, Permissions.RequestRefund);
		var category = await Insert(new FeeCategory(Guid.NewGuid(), tenantId, f.Year.Id, "Refund fees", null));
		var income = await Insert(new TransactionCategory(Guid.NewGuid(), tenantId, f.Year.Id, TransactionType.Income, "Refund income", null));
		var head = await Insert(new FeeHead(Guid.NewGuid(), tenantId, f.Year.Id, category.Id, "Tuition", null, true, null, income.Id));
		var policyId = Guid.NewGuid(); var versionId = Guid.NewGuid();
		var policy = new RefundPolicy(policyId, tenantId, f.Year.Id, new RefundPolicyUpdateModel(
			"Assigned policy", "PARENT-REFUND", null, RefundPolicyStatus.Active, false, RefundFeeHeadScope.AllFeeHeads,
			[], true, false, true, mode, mode == RefundAmountMode.Percentage ? 50m : null,
			mode == RefundAmountMode.FixedAmount ? 100m : null, null, null, RoundingMethodType.None,
			RefundDeductionType.None, null, null, null, [RefundPayoutMethod.Cash], null));
		await Insert(policy);
		await Insert(new FinancePolicyVersion(versionId, tenantId, "refund", policyId, f.Year.Id, policy.Version,
			policy.PolicyName, policy.Status.ToString(), JsonSerializer.Serialize(policy.ToSnapshot()), "created", "Fixture"));
		var assignment = await Insert(new StudentFeeStructure(Guid.NewGuid(), tenantId, f.Student.Id, Guid.NewGuid(), f.Year.Id,
			null, null, ConcessionFeeHeadScope.EntireFeeStructure, [], refundPolicyId: policyId, refundPolicyVersionId: versionId));
		Guid? accountId = null;
		if (payerKind != "student")
		{
			var account = new FinanceFeeAccount(Guid.NewGuid(), tenantId, Guid.NewGuid(), Guid.NewGuid(), f.Year.Id,
				f.Student.AcademicBoardId!.Value, f.Student.AcademicMediumId, null, f.Enrollment.StandardId!.Value, "Student", studentId: f.Student.Id);
			account.CaptureAssignment(Guid.NewGuid(), 1, JsonSerializer.Serialize(new { Policies = new[] { new { Family = "refund", PolicyId = policyId, VersionId = versionId } } }), "refund-fixture");
			account.BindStudentAssignment(assignment.Id); await Insert(account);
			accountId = account.Id; assignment.BindFinanceFeeAccount(account.Id);
		}
		var due = new DueFee(Guid.NewGuid(), tenantId, f.Student.Id, head.Id, MonthEnum.July, 2026, false, 1000m, DateTime.Today);
		if (accountId.HasValue) due.BindFinanceFeeAccount(accountId.Value);
		due.BindAssignment(assignment.Id); due.ApplyPayment(1000m); await Insert(due);
		var chargeCategory = await Insert(new ChargeCategory(Guid.NewGuid(), tenantId, f.Year.Id, "Transport", false, null));
		var charge = await Insert(NewFamilyCharge(f, tenantId, chargeCategory.Id, "Transport charge", 200m));
		var tx = await Insert(new FeeRecordMetadata(Guid.NewGuid(), tenantId, f.Student.Id, f.Year.Id, 1200d, "REF-PAYMENT", FeeCollectionStatus.Paid,
			financeFeeAccountId: payerKind == "account" ? accountId : null));
		var allocation = await Insert(new FeeCollectionDueFeeAllocation(Guid.NewGuid(), tenantId, tx.Id, due.Id, 1000d));
		var chargeAllocation = await Insert(new ChargeCollectionRecord(Guid.NewGuid()) { TenantId = tenantId, StudentId = f.Student.Id, ChargeId = charge.Id, FeeRecordMetadataId = tx.Id, Amount = 200d, PaymentDate = DateTimeOffset.UtcNow });
		var receipt = await Insert(new FeePaymentReceipt(Guid.NewGuid()) { TenantId = tenantId, StudentId = f.Student.Id,
			AcademicYearId = f.Year.Id, FeeCollectionTransactionId = tx.Id, ReceiptNumber = "REF-COMBINED", ReceiptDate = new DateOnly(2026, 7, 1),
			ReceiptScope = FeeReceiptScope.Combined, ReceiptKind = FeeReceiptKind.FinalFee, VerificationStatus = FeeReceiptVerificationStatus.Verified, TotalPaidAmount = 1200d });
		if (payerKind == "account")
		{
			receipt.SetFinanceFeeAccount(accountId!.Value);
			await GetRequiredService<IRepository<FeePaymentReceipt, Guid>>().UpdateAsync(receipt, autoSave: true);
		}
			return new { f, due, charge, allocation, chargeAllocation, receipt, versionId, accountId };
		});
		using var identity = FinanceIdentity(fixture.f.User.Id);
		var service = FamilyService<FeeRefundAppService>();
		var eligibility = await service.FamilyRefundEligibilityAsync(fixture.f.Student.Id, fixture.f.Year.Id);
		eligibility.RefundPolicyVersionId.ShouldBe(fixture.versionId);
		eligibility.FinanceFeeAccountId.ShouldBe(fixture.accountId);
		eligibility.FormRules!.AmountMode.ShouldBe(mode.ToString());
		eligibility.FormRules.CanEditLineAmounts.ShouldBe(mode == RefundAmountMode.ManualUpToEligible);
		eligibility.FeeHeads.SelectMany(x => x.Months).Count().ShouldBe(2);
		eligibility.FeeHeads.SelectMany(x => x.Months).Single(x => x.ChargeId.HasValue).ChargeId.ShouldBe(fixture.charge.Id);
		eligibility.FeeHeads.SelectMany(x => x.Months).All(x => x.PaymentDate == new DateOnly(2026, 7, 1)).ShouldBeTrue();
		var input = new MobileRefundInput { OriginalReceiptId = fixture.receipt.Id, RefundMethod = "Cash", Reason = "Refund selected paid items",
			PaymentDetails = new() { ["receivedBy"] = "Parent" }, Lines = [
				new() { DueFeeId = fixture.due.Id, SourceAllocationId = fixture.allocation.Id, Amount = "100.00" },
				new() { ChargeId = fixture.charge.Id, SourceAllocationId = fixture.chargeAllocation.Id, Amount = "50.00" }] };
		// Preview and create are separate requests/contexts in the real API.
		var preview = await FinanceRequestAsync(() => service.FamilyRefundPreviewAsync(fixture.f.Student.Id, fixture.f.Year.Id, input));
		preview.RequestedAmount.ShouldBe(requested); preview.MaximumAllowedAmount.ShouldBe(expected);
		input.Lines[1].DueFeeId = fixture.due.Id;
		await Should.ThrowAsync<BusinessException>(() => service.FamilyRefundPreviewAsync(fixture.f.Student.Id, fixture.f.Year.Id, input));
		input.Lines[1].DueFeeId = null;
		input.PreviewFingerprint = preview.PreviewFingerprint;
		input.IdempotencyKey = Guid.NewGuid().ToString("N");
		await GrantFinancePermissionAsync(fixture.f.User.Id, Permissions.SubmitRefund);
		var created = await service.FamilyRefundCreateAsync(fixture.f.Student.Id, fixture.f.Year.Id, input);
		created.Status.ShouldBe("Submitted"); created.CalculatedAmount.ShouldBe(expected);
		var replay = await service.FamilyRefundCreateAsync(fixture.f.Student.Id, fixture.f.Year.Id, input);
		replay.Id.ShouldBe(created.Id);
		fixture.due.RefundedAmount.ShouldBe(0m, "Submitting a request must not record a payout.");
	});
}
