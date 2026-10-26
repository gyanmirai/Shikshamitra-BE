using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.FeeAdjustment;
using ShikshaMitra.Finance.Fees.FeeAdjustment.Aggregates;
using ShikshaMitra.Finance.Fees.FeeAdjustment.Entities;
using ShikshaMitra.Finance.Fees.FeeAdjustment.Repositories;
using ShikshaMitra.Finance.Fees.FeeCollection;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Collections;

public partial class LateFeeCollectionRuntime_Tests
{
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
	public async Task Ledger_Adjustment_Eligibility_Uses_Fee_Head_Academic_Year_And_Matching_Reservations()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var (year, student, head, dueId, _) = await SeedStudentWithOverdueDueFeeAsync(1, false);
			var (otherYear, _, otherHead, _, _) = await SeedStudentWithOverdueDueFeeAsync(1, false, academicYearName: "Other ledger year");
			var due = await _dueFeeRepo.GetAsync(dueId);
			due.ApplyPayment(200m); due.ApplyAdjustmentCredit(50m);
			await _dueFeeRepo.UpdateAsync(due, autoSave: true);
			// The same calendar month/year belongs to a different academic year through its fee head.
			var other = new DueFee(Guid.NewGuid(), TestTenantId, student, otherHead,
				due.Month, due.Year, false, 900m, due.DueDate);
			other.ApplyPayment(400m); other.ApplyAdjustmentCredit(100m);
			await _dueFeeRepo.InsertAsync(other, autoSave: true);
			foreach (var (source, academicYear, amount) in new[] { (due, year, 150d), (other, otherYear, 50d) })
			{
				var request = new FeeAdjustmentRequest(Guid.NewGuid(), TestTenantId, student, academicYear,
					Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Reserve source",
					$"LEDGER-{Guid.NewGuid():N}", amount);
				request.SetSourceLines([new FeeAdjustmentSourceLine(Guid.NewGuid(), request.Id,
					source.Id, source.FeeHeadId, source.Month, source.Year, amount)]);
				await GetRequiredService<IFeeAdjustmentRepository>().InsertAsync(request, autoSave: true);
			}
			var service = GetRequiredService<IFeeAdjustmentAppService>();
			var eligibility = await service.GetEligibilityAsync(student, year);
			eligibility.TotalAdjustable.ShouldBe(4600d);
			eligibility.TotalPaid.ShouldBe(200d);
			eligibility.TotalAlreadyAdjusted.ShouldBe(50d);
			eligibility.FeeHeads.Single().FeeHeadId.ShouldBe(head);
			eligibility.FeeHeads.Single().Months.Single().DueFeeId.ShouldBe(due.Id);
			var previous = await service.GetEligibilityAsync(student, otherYear);
			previous.TotalAdjustable.ShouldBe(350d);
			previous.TotalPaid.ShouldBe(400d);
			previous.TotalAlreadyAdjusted.ShouldBe(100d);
			previous.FeeHeads.Single().FeeHeadId.ShouldBe(otherHead);
			var empty = await service.GetEligibilityAsync(student, Guid.NewGuid());
			empty.FeeHeads.ShouldBeEmpty();
			empty.TotalPaid.ShouldBe(0d); empty.TotalAlreadyAdjusted.ShouldBe(0d); empty.TotalAdjustable.ShouldBe(0d);
		}
	}
}
