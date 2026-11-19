using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.FeeAdjustment;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Collections;

public partial class LateFeeCollectionRuntime_Tests
{
    [Fact]
    public async Task Adjustment_eligibility_lists_overdue_outstanding_without_paid_credit()
    {
        using (_currentTenant.Change(TestTenantId))
        {
            var (year, student, _, dueId, _) = await SeedStudentWithOverdueDueFeeAsync(1, false);

            var eligibility = await GetRequiredService<IFeeAdjustmentAppService>()
                .GetEligibilityAsync(student, year);
            var line = eligibility.FeeHeads.SelectMany(head => head.Months)
                .Single(month => month.DueFeeId == dueId);

            line.Kind.ShouldBe(FeeAdjustmentKind.UnpaidTransfer);
            line.Status.ShouldBe(FeeCollectionStatus.Overdue);
            line.Adjustable.ShouldBeGreaterThan(0d);
            line.Adjustable.ShouldBe(line.OutstandingAmount);
        }
    }
}
