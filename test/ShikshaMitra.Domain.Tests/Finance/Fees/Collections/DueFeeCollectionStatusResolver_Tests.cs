using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Settings.PaymentTerms;
using System;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Collections;

public class DueFeeCollectionStatusResolver_Tests
{
	private static readonly DateTime Today = new(2026, 10, 1);
	private static DueFee Create(DateTime dueDate, DateTime? demandDate = null, DateTime? scheduleDueDate = null) =>
		new(Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), MonthEnum.June, 2026, false, 1000m,
			dueDate, demandDate: demandDate, scheduleDueDate: scheduleDueDate,
			scheduleSource: demandDate.HasValue ? ScheduleSource.PaymentTerm : ScheduleSource.Legacy);

	[Theory]
	[InlineData(-1, true, FeeCollectionStatus.Overdue)]
	[InlineData(0, false, FeeCollectionStatus.Pending)]
	[InlineData(1, false, FeeCollectionStatus.Pending)]
	public void Due_Date_Controls_Overdue_Instead_Of_Display_Month(int offset, bool overdue, FeeCollectionStatus status)
	{
		var due = Create(Today.AddDays(offset).AddHours(13));
		Assert.Equal(overdue, DueFeeCollectionStatusResolver.OverdueOn(Today.AddHours(20)).Compile()(due));
		Assert.Equal(status, DueFeeCollectionStatusResolver.Resolve(due, Today.AddHours(20), 0));
	}

	[Fact]
	public void Demand_Date_And_Scheduled_Due_Date_Are_Respected()
	{
		var notGenerated = Create(Today.AddDays(-10), Today.AddDays(1));
		var dueToday = Create(Today.AddDays(-10), Today.AddDays(-2), Today);
		foreach (var due in new[] { notGenerated, dueToday })
		{
			Assert.False(DueFeeCollectionStatusResolver.OverdueOn(Today).Compile()(due));
			Assert.Equal(FeeCollectionStatus.Pending, DueFeeCollectionStatusResolver.Resolve(due, Today, 0));
		}
	}

	[Fact]
	public void Demand_Time_Today_Does_Not_Exclude_An_Overdue_Installment()
	{
		var due = Create(Today.AddDays(-1), Today.AddHours(12), Today.AddDays(-1));
		// Simulate an older persisted value materialized without constructor normalization.
		typeof(DueFee).GetProperty(nameof(DueFee.DemandDate))!.SetValue(due, Today.AddHours(12));
		Assert.Equal(Today.AddHours(12), due.DemandDate);
		Assert.True(DueFeeCollectionStatusResolver.OverdueOn(Today).Compile()(due));
		Assert.Equal(FeeCollectionStatus.Overdue, DueFeeCollectionStatusResolver.Resolve(due, Today, 0));
	}

	[Fact]
	public void Partially_Settled_Overdue_Bill_Uses_Only_Remaining_Balance()
	{
		var due = Create(Today.AddDays(-1)); due.ApplyPayment(400m); due.ApplyConcession(100m);
		Assert.True(DueFeeCollectionStatusResolver.OverdueOn(Today).Compile()(due));
		Assert.Equal(500m, due.OutstandingAmount);
		Assert.Equal(FeeCollectionStatus.Overdue, DueFeeCollectionStatusResolver.Resolve(due, Today, 400));
		due.ApplyPayment(500m);
		Assert.False(DueFeeCollectionStatusResolver.OverdueOn(Today).Compile()(due));
		Assert.Equal(FeeCollectionStatus.InReconciliation, DueFeeCollectionStatusResolver.Resolve(due, Today, 400));
		Assert.Equal(FeeCollectionStatus.Paid, DueFeeCollectionStatusResolver.Resolve(due, Today, 0));
	}

	[Fact]
	public void Refunds_Do_Not_Reopen_A_Fully_Paid_Overdue_Installment()
	{
		var due = Create(Today.AddDays(-30));
		due.ApplyPayment(1000m);
		due.ApplyRefund(300m);
		Assert.Equal(0m, due.OutstandingAmount);
		Assert.Equal(FeeStatus.Paid, due.Status);
		Assert.False(DueFeeCollectionStatusResolver.OverdueOn(Today).Compile()(due));
		Assert.Equal(FeeCollectionStatus.Paid, DueFeeCollectionStatusResolver.Resolve(due, Today, 0));
		Assert.Equal(700m, due.RefundableBalance);

		due.ApplyRefund(700m);
		Assert.Equal(0m, due.OutstandingAmount);
		Assert.Equal(FeeCollectionStatus.Paid, DueFeeCollectionStatusResolver.Resolve(due, Today, 0));
		Assert.Equal(0m, due.RefundableBalance);

		due.ReverseRefund(1000m);
		Assert.Equal(0m, due.OutstandingAmount);
		Assert.Equal(FeeCollectionStatus.Paid, DueFeeCollectionStatusResolver.Resolve(due, Today, 0));
		Assert.Equal(1000m, due.RefundableBalance);
	}

	[Fact]
	public void Refunding_A_Partial_Payment_Preserves_The_Original_Unpaid_Remainder()
	{
		var due = Create(Today.AddDays(-30));
		due.ApplyPayment(400m);
		due.ApplyRefund(400m);
		Assert.Equal(600m, due.OutstandingAmount);
		Assert.Equal(FeeStatus.Partial, due.Status);
		Assert.Equal(FeeCollectionStatus.Overdue, DueFeeCollectionStatusResolver.Resolve(due, Today, 0));
		Assert.Equal(FeeCollectionStatus.Partial, DueFeeCollectionStatusResolver.Resolve(due, Today.AddDays(-31), 0));
	}

	[Fact]
	public void NonOverdue_Status_Preserves_Reconciliation_And_Partial_Settlement()
	{
		var due = Create(Today); due.ApplyPayment(200m);
		Assert.Equal(FeeCollectionStatus.InReconciliation, DueFeeCollectionStatusResolver.Resolve(due, Today, 200));
		Assert.Equal(FeeCollectionStatus.Partial, DueFeeCollectionStatusResolver.Resolve(due, Today, 0));
		due.ReversePayment(200m);
		Assert.Equal(FeeCollectionStatus.Pending, DueFeeCollectionStatusResolver.Resolve(due, Today, 0));
	}
}
