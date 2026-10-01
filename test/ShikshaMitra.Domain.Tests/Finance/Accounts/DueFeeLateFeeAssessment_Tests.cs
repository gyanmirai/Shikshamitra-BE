using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using System;
using Xunit;

namespace ShikshaMitra.Finance.Accounts;

public class DueFeeLateFeeAssessment_Tests
{
	private static DueFee Create() => new(Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(),
		MonthEnum.June, 2026, false, 1000m, new DateTime(2026, 6, 10));

	[Fact]
	public void Principal_Settlement_And_Reversal_Do_Not_Settle_Or_Duplicate_An_Assessed_Charge()
	{
		var due = Create();
		due.PreserveLateFeeAssessment(100m, DateTimeOffset.UtcNow, Guid.NewGuid(), "Flat late fee");
		due.ApplyPayment(1000m);
		Assert.Equal(0m, due.OutstandingAmount);
		Assert.Equal(100m, due.GetAssessedLateFeeAmount());
		due.ReversePayment(1000m);
		Assert.Equal(1000m, due.OutstandingAmount);
		Assert.Equal(100m, due.GetAssessedLateFeeAmount());
	}

	[Fact]
	public void An_Accrued_Assessment_Cannot_Be_Reduced_By_A_Lower_Recalculation()
	{
		var due = Create();
		var version = Guid.NewGuid();
		due.PreserveLateFeeAssessment(125.50m, DateTimeOffset.UtcNow, version, "Original calculation");
		due.PreserveLateFeeAssessment(25m, DateTimeOffset.UtcNow, version, "Principal reduced");
		Assert.Equal(125.50m, due.GetAssessedLateFeeAmount());
		Assert.Equal("Original calculation", due.GetLateFeeAssessmentExplanation());
		due.PreserveLateFeeAssessment(150m, DateTimeOffset.UtcNow, version, "Additional accrual");
		Assert.Equal(150m, due.GetAssessedLateFeeAmount());
		Assert.Equal(0m, due.PaidAmount);
		Assert.Equal(1000m, due.OutstandingAmount);
	}

	[Fact]
	public void Assessment_Requires_Nonnegative_Money_And_A_Policy_Version()
	{
		var due = Create();
		Assert.Throws<ArgumentOutOfRangeException>(() =>
			due.PreserveLateFeeAssessment(-1m, DateTimeOffset.UtcNow, Guid.NewGuid(), "Invalid"));
		Assert.Throws<ArgumentException>(() =>
			due.PreserveLateFeeAssessment(100m, DateTimeOffset.UtcNow, Guid.Empty, "Invalid"));
	}
}
