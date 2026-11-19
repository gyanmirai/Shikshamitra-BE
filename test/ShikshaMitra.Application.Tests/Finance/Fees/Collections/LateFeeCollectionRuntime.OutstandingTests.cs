using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Settings.PaymentTerms;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Collections;

public partial class LateFeeCollectionRuntime_Tests
{
	[Theory]
	[InlineData(0, 0, 13224, 29776, 3306)]
	[InlineData(1000, 500, 13224, 28276, 3306)]
	[InlineData(0, 0, 16530, 26470, 0)]
	public async Task Summary_Pending_Includes_Future_Payment_Term_Installments_And_Matches_Collection(
		int concession, int adjustment, int paid, int expectedOutstanding, int expectedOverdue)
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var (year, student, head, currentId, _) = await SeedStudentWithOverdueDueFeeAsync(
				1, true, enableLateFee: false, principalAmount: 16530m);
			var current = await _dueFeeRepo.GetAsync(currentId);
			current.ApplyPayment(paid);
			await _dueFeeRepo.UpdateAsync(current, autoSave: true);

			var demandDate = DateTime.UtcNow.Date.AddMonths(1);
			var dueDate = demandDate.AddDays(10);
			var future = new DueFee(Guid.NewGuid(), TestTenantId, student, head,
				(MonthEnum)(dueDate.Month - 1), dueDate.Year, false, 26470m, dueDate,
				installmentSequence: 2, installmentName: "Future installment",
				demandDate: demandDate, scheduleDueDate: dueDate, scheduleSource: ScheduleSource.PaymentTerm);
			future.BindAssignment(current.StudentFeeStructureId!.Value);
			if (concession > 0) future.ApplyConcession(concession);
			if (adjustment > 0) future.ApplyAdjustmentCredit(adjustment);
			await _dueFeeRepo.InsertAsync(future, autoSave: true);

			var summary = await GetSummaryRowAsync(year, student);
			summary.TotalFees.ShouldBe(43000d);
			summary.PaidFees.ShouldBe(paid);
			summary.PendingFees.ShouldBe(expectedOutstanding);
			summary.OverdueFees.ShouldBe(expectedOverdue);
			summary.Status.ShouldBe(expectedOverdue > 0 ? FeeCollectionStatus.Overdue : FeeCollectionStatus.Pending);

			var details = await _feeCollections.GetStudentFeeDetailsAsync(student, year);
			details.FeeOverview.PendingFee.ShouldBe(expectedOutstanding);
			var form = await _feeCollections.GetCollectFormAsync(student, year);
			form.Summary.PendingFee.ShouldBe(expectedOutstanding);
			form.Ledger.SelectMany(group => group.Items).ShouldContain(line => line.DueFeeId == future.Id);
		}
	}
}
