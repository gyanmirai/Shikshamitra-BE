using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.FeeRefund;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Collections;

public partial class LateFeeCollectionRuntime_Tests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Refund_eligibility_keeps_collected_1500_after_remaining_9500_late_fee_is_waived(bool includePaidLateFee)
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var (yearId, studentId, _, dueId, _) = await SeedStudentWithOverdueDueFeeAsync(550, true);
			var due = await _dueFeeRepo.GetAsync(dueId);
			var assignment = await _studentFeeStructureRepo.GetAsync(due.StudentFeeStructureId!.Value);
			var policy = new RefundPolicy(Guid.NewGuid(), TestTenantId, yearId, new RefundPolicyUpdateModel(
				"Paid late fee refund", $"LATE-{Guid.NewGuid():N}", null, RefundPolicyStatus.Active,
				false, RefundFeeHeadScope.AllFeeHeads, [], true, includePaidLateFee, true,
				RefundAmountMode.RemainingPaidAmount, null, null, null, null, RoundingMethodType.None,
				RefundDeductionType.None, null, null, null, [RefundPayoutMethod.Cash], null));
			await GetRequiredService<IRepository<RefundPolicy, Guid>>().InsertAsync(policy, autoSave: true);
			var version = new FinancePolicyVersion(Guid.NewGuid(), TestTenantId, "refund", policy.Id, yearId,
				policy.Version, policy.PolicyName, policy.Status.ToString(), JsonSerializer.Serialize(policy.ToSnapshot()),
				"created", "Paid plus waived late-fee regression");
			await _policyVersionRepo.InsertAsync(version, autoSave: true);
			assignment.ChangeAssignedPolicies(assignment.ConcessionPolicyId, assignment.ConcessionPolicyVersionId,
				assignment.ConcessionFeeHeadScope, [], assignment.CollectionPolicyId!.Value,
				assignment.CollectionPolicyVersionId!.Value, policy.Id, version.Id,
				assignment.ProrationPolicyId, assignment.ProrationPolicyVersionId,
				"Pin paid late-fee refund policy", FakeAdminUserId, DateTime.UtcNow);
			await _studentFeeStructureRepo.UpdateAsync(assignment, autoSave: true);

			var originalForm = await _feeCollections.GetCollectFormAsync(studentId, yearId);
			originalForm.Charges.Single(x => x.SourceId == dueId).Amount.ShouldBe(11_000d);
			var collection = await _feeCollections.CollectFeeAsync(new CollectFeeInput
			{
				IdempotencyKey = Guid.NewGuid().ToString("N"), StudentId = studentId, AcademicYearId = yearId,
				LateFeePayments = [new() { DueFeeId = dueId, Amount = 1_500d }],
				Payments = [new() { PaymentMethod = PaymentMethodEnum.Cash, Amount = 1_500d }]
			});
			collection.PaidLateFees.Single().AmountPaid.ShouldBe(1_500d);
			var waiver = await _waivers.ApplyAsync(new ApplyFeeWaiverInput
			{
				SourceType = "LateFee", SourceId = dueId, Reason = "Waive the unpaid balance only",
				IdempotencyKey = Guid.NewGuid().ToString("N")
			});
			waiver.WaivedAmount.ShouldBe(9_500m);
			var form = await _feeCollections.GetCollectFormAsync(studentId, yearId);
			var charge = form.Charges.Single(x => x.SourceId == dueId);
			charge.Amount.ShouldBe(11_000d);
			charge.PaidAmount.ShouldBe(1_500d);
			charge.WaivedAmount.ShouldBe(9_500d);
			charge.PendingAmount.ShouldBe(0d);

			var sources = await GetRequiredService<PaidLateFeeRefundService>().GetSourcesAsync(studentId, yearId,
				assignmentId: assignment.Id);
			var paidSource = sources.Single();
			paidSource.DueFeeId.ShouldBe(dueId);
			paidSource.Paid.ShouldBe(1_500m);
			paidSource.Available.ShouldBe(1_500m);
			var eligibility = await GetRequiredService<IFeeRefundAppService>().GetEligibilityAsync(studentId, yearId);
			var lateRows = eligibility.FeeHeads.SelectMany(x => x.Months).Where(x => x.LateFeeDueFeeId.HasValue).ToList();
			if (!includePaidLateFee)
			{
				lateRows.ShouldBeEmpty();
				eligibility.TotalRefundable.ShouldBe(0m);
				return;
			}
			var late = lateRows.Single();
			late.LateFeeDueFeeId.ShouldBe(dueId);
			late.SourceAllocationId.ShouldBe(paidSource.AllocationId);
			late.SourceReceiptId.ShouldBe(paidSource.ReceiptId);
			late.Paid.ShouldBe(1_500m);
			late.Refundable.ShouldBe(1_500m);
			eligibility.TotalRefundable.ShouldBe(1_500m);
			(await _dueFeeRepo.GetAsync(dueId)).PaidAmount.ShouldBe(0m);
		}
	}
}
