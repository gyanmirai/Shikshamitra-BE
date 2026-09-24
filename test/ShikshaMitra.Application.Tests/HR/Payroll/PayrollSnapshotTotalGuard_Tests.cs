using System;
using System.Collections.Generic;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.SalarySlips;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class PayrollSnapshotTotalGuard_Tests
{
	[Fact]
	public void Matching_Snapshot_Line_Totals_Should_Pass()
	{
		var snapshot = CreateSnapshot();
		var earnings = new List<SalarySlipLineItem> { new("Basic", 100m) };
		var deductions = new List<SalarySlipLineItem> { new("Tax", 10m) };

		PayrollSnapshotTotalGuard.EnsureMatchesLineItems(snapshot, earnings, deductions);
	}

	[Fact]
	public void Divergent_Snapshot_Line_Totals_Should_Be_Rejected()
	{
		var snapshot = CreateSnapshot();
		var earnings = new List<SalarySlipLineItem> { new("Basic", 99m) };
		var deductions = new List<SalarySlipLineItem> { new("Tax", 10m) };

		var exception = Should.Throw<BusinessException>(() =>
			PayrollSnapshotTotalGuard.EnsureMatchesLineItems(snapshot, earnings, deductions));

		exception.Code.ShouldBe(HRErrorCodes.PayrollBlockingIssuesBeforeLock);
	}

	[Fact]
	public void Additional_earnings_increase_net_but_not_regular_gross()
	{
		var snapshot = CreateSnapshot();
		snapshot.NetPay = 115m;
		var earnings = new List<SalarySlipLineItem> { new("Basic", 100m), new("Bonus", 25m, isTemp: true) };
		var deductions = new List<SalarySlipLineItem> { new("Tax", 10m) };
		PayrollSnapshotTotalGuard.EnsureMatchesLineItems(snapshot, earnings, deductions);
		snapshot.GrossSalary = 125m;
		Should.Throw<BusinessException>(() => PayrollSnapshotTotalGuard.EnsureMatchesLineItems(snapshot, earnings, deductions));
	}

	private static PayrollRunItemSnapshot CreateSnapshot() =>
		new(Guid.NewGuid(), null, "2026-05", Guid.NewGuid())
		{
			GrossSalary = 100m,
			TotalDeduction = 10m,
			NetPay = 90m
		};
}
