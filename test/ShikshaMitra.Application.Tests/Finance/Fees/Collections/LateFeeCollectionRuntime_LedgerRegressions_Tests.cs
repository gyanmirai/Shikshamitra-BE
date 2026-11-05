using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.FeeAdjustment;
using ShikshaMitra.Finance.Fees.FeeAdjustment.Aggregates;
using ShikshaMitra.Finance.Fees.FeeAdjustment.Entities;
using ShikshaMitra.Finance.Fees.FeeAdjustment.Repositories;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Collections;

public partial class LateFeeCollectionRuntime_Tests
{
	[Fact]
	public async Task Ledger_Adjustment_Eligibility_Excludes_Unverified_Provisional_Payment()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var (year, student, _, dueId, _) = await SeedStudentWithOverdueDueFeeAsync(1, false);
			var due = await _dueFeeRepo.GetAsync(dueId);
			due.ApplyPayment(due.OriginalAmount);
			due.ApplyConcession(20m);
			await _dueFeeRepo.UpdateAsync(due, autoSave: true);
			var payment = new FeeRecordMetadata(Guid.NewGuid(), TestTenantId, student, year,
				(double)due.PaidAmount, $"UNVERIFIED-{Guid.NewGuid():N}", FeeCollectionStatus.Paid);
			await GetRequiredService<IRepository<FeeRecordMetadata, Guid>>().InsertAsync(payment, autoSave: true);
			await GetRequiredService<IRepository<FeeCollectionDueFeeAllocation, Guid>>().InsertAsync(
				new FeeCollectionDueFeeAllocation(Guid.NewGuid(), TestTenantId, payment.Id,
					due.Id, (double)due.PaidAmount), autoSave: true);
			await GetRequiredService<IRepository<FeePaymentReceipt, Guid>>().InsertAsync(
				new FeePaymentReceipt(Guid.NewGuid())
				{
					TenantId = TestTenantId, StudentId = student, AcademicYearId = year,
					FeeCollectionTransactionId = payment.Id, ReceiptNumber = $"KFE-{Guid.NewGuid():N}",
					ReceiptKind = FeeReceiptKind.ProvisionalFee, ReceiptScope = FeeReceiptScope.Fee,
					VerificationStatus = FeeReceiptVerificationStatus.PendingVerification,
					TotalPaidAmount = (double)due.PaidAmount,
					ReceiptDate = DateOnly.FromDateTime(DateTime.UtcNow),
				}, autoSave: true);

			var eligibility = await GetRequiredService<IFeeAdjustmentAppService>()
				.GetEligibilityAsync(student, year);
			eligibility.TotalPaid.ShouldBe((double)due.PaidAmount);
			eligibility.TotalAdjustable.ShouldBe(0d);
			eligibility.FeeHeads.ShouldBeEmpty();
		}
	}

	[Theory]
	[InlineData(100d, 0d, 0d, FeeCollectionStatus.InReconciliation, 0d)]
	[InlineData(40d, 30d, 0d, FeeCollectionStatus.InReconciliation, 30d)]
	[InlineData(0d, 100d, 0d, FeeCollectionStatus.Paid, 0d)]
	[InlineData(0d, 30d, 0d, FeeCollectionStatus.Partial, 70d)]
	[InlineData(0d, 0d, 100d, FeeCollectionStatus.Pending, 100d)]
	public async Task Ledger_Details_Charge_Status_Distinguishes_Uncleared_And_Reversed_Payments(
		double reconciliation, double cleared, double reversed, FeeCollectionStatus status, double pending)
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var (year, studentId, _, _, _) = await SeedStudentWithOverdueDueFeeAsync(1, false);
			var student = await _studentRepo.GetAsync(studentId);
			var charge = new Charge(Guid.NewGuid(), TestTenantId, year, Guid.NewGuid(), "Test charge", Guid.NewGuid(),
				studentId, student.AcademicBoardId!.Value, student.AcademicMediumId,
				Guid.NewGuid(), Guid.NewGuid(), "Ledger reconciliation", 100m,
				DateTimeOffset.UtcNow, ChargePriority.Medium, ChargeStatus.Pending, Guid.NewGuid());
			charge.Approve(Guid.NewGuid());
			await GetRequiredService<IRepository<Charge, Guid>>().InsertAsync(charge, autoSave: true);
			foreach (var (amount, paymentStatus) in new[]
			{
				(reconciliation, FeeCollectionStatus.InReconciliation),
				(cleared, FeeCollectionStatus.Paid), (reversed, FeeCollectionStatus.Reversed)
			})
			{
				if (amount == 0d) continue;
				var metadata = new FeeRecordMetadata(Guid.NewGuid(), TestTenantId, studentId, year,
					amount, $"LEDGER-{Guid.NewGuid():N}", paymentStatus);
				await GetRequiredService<IRepository<FeeRecordMetadata, Guid>>().InsertAsync(metadata, autoSave: true);
				await GetRequiredService<IRepository<ChargeCollectionRecord, Guid>>().InsertAsync(
					new ChargeCollectionRecord(Guid.NewGuid())
					{
						TenantId = TestTenantId, StudentId = studentId, ChargeId = charge.Id,
						FeeRecordMetadataId = metadata.Id, Amount = amount, PaymentDate = DateTimeOffset.UtcNow,
					}, autoSave: true);
			}
			var detail = (await _feeCollections.GetStudentFeeDetailsAsync(studentId, year)).Charges!.Single();
			detail.Status.ShouldBe(status);
			detail.ReconciliationAmount.ShouldBe((decimal)reconciliation);
			// Existing PaidAmount includes uncleared allocations; PendingAmount is the collectible remainder.
			detail.PaidAmount.ShouldBe(reconciliation + cleared);
			detail.PendingAmount.ShouldBe(pending);
			detail.CanWaive.ShouldBe(pending > 0d);
		}
	}

	[Fact]
	public async Task Ledger_Details_Late_Fee_Distinguishes_Uncleared_Allocation_From_Paid()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var (year, student, _, due, _) = await SeedStudentWithOverdueDueFeeAsync(10, true, maximumLateFeeAmount: 200);
			var metadata = new FeeRecordMetadata(Guid.NewGuid(), TestTenantId, student, year,
				200d, $"LEDGER-LATE-{Guid.NewGuid():N}", FeeCollectionStatus.InReconciliation);
			await GetRequiredService<IRepository<FeeRecordMetadata, Guid>>().InsertAsync(metadata, autoSave: true);
			await GetRequiredService<IRepository<LateFeeCollectionAllocation, Guid>>().InsertAsync(
				new LateFeeCollectionAllocation(Guid.NewGuid(), TestTenantId, metadata.Id, due, 200d), autoSave: true);
			var detail = (await _feeCollections.GetStudentFeeDetailsAsync(student, year)).Charges!.Single();
			detail.Status.ShouldBe(FeeCollectionStatus.InReconciliation);
			detail.ReconciliationAmount.ShouldBe(200m);
			detail.PaidAmount.ShouldBe(200d);
			detail.PendingAmount.ShouldBe(0d);
			detail.CanWaive.ShouldBeFalse();
		}
	}

	[Fact]
	public async Task Ledger_Adjustment_Eligibility_Uses_Settled_Paid_Credit_And_Reservation()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var (year, student, head, dueId, _) = await SeedStudentWithOverdueDueFeeAsync(1, false);
			var due = await _dueFeeRepo.GetAsync(dueId);
			due.ApplyAdjustmentCredit(50m);
			due.ApplyPayment(due.OriginalAmount - 50m);
			due.ApplyConcession(200m);
			due.ReservePaidCredit(150m);
			await _dueFeeRepo.UpdateAsync(due, autoSave: true);
			var payment = new FeeRecordMetadata(Guid.NewGuid(), TestTenantId, student, year,
				(double)due.PaidAmount, $"ADJ-{Guid.NewGuid():N}", FeeCollectionStatus.Paid);
			await GetRequiredService<IRepository<FeeRecordMetadata, Guid>>().InsertAsync(payment, autoSave: true);
			await GetRequiredService<IRepository<FeeCollectionDueFeeAllocation, Guid>>().InsertAsync(
				new FeeCollectionDueFeeAllocation(Guid.NewGuid(), TestTenantId, payment.Id,
					due.Id, (double)due.PaidAmount), autoSave: true);
			await GetRequiredService<IRepository<FeePaymentReceipt, Guid>>().InsertAsync(
				new FeePaymentReceipt(Guid.NewGuid())
				{
					TenantId = TestTenantId, StudentId = student, AcademicYearId = year,
					FeeCollectionTransactionId = payment.Id, ReceiptNumber = $"ADJ-FIN-{Guid.NewGuid():N}",
					ReceiptKind = FeeReceiptKind.FinalFee, ReceiptScope = FeeReceiptScope.Fee,
					VerificationStatus = FeeReceiptVerificationStatus.Verified,
					TotalPaidAmount = (double)due.PaidAmount,
					ReceiptDate = DateOnly.FromDateTime(DateTime.UtcNow),
				}, autoSave: true);
			var service = GetRequiredService<IFeeAdjustmentAppService>();
			var eligibility = await service.GetEligibilityAsync(student, year);
			eligibility.TotalAdjustable.ShouldBe(50d);
			eligibility.TotalPaid.ShouldBe((double)(due.OriginalAmount - 50m));
			eligibility.TotalAlreadyAdjusted.ShouldBe(50d);
			eligibility.FeeHeads.Single().FeeHeadId.ShouldBe(head);
			eligibility.FeeHeads.Single().Months.Single().DueFeeId.ShouldBe(due.Id);
			await Should.ThrowAsync<Volo.Abp.Authorization.AbpAuthorizationException>(
				() => service.GetEligibilityAsync(student, Guid.NewGuid()));
		}
	}
}
