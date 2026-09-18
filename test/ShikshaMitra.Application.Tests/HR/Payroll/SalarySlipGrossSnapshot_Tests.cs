using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.SalarySlips;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class SalarySlipGrossSnapshot_Tests
{
	[Theory]
	[InlineData("gross")]
	[InlineData("type")]
	[InlineData("rate")]
	[InlineData("basis")]
	public void Draft_must_refresh_when_snapshot_inputs_change_without_changing_paid_amounts(string change)
	{
		var original = new SalarySlipLineItem("Basic", 0m)
		{
			FullMonthAmount = 12000m,
			FormulaType = "PercentageOfGross",
			FormulaValue = 60m
		};
		var slip = new SalarySlip(Guid.NewGuid(), null, Guid.NewGuid(), "2026-09", [original], [], DateTime.UtcNow);
		var snapshot = new PayrollRunItemSnapshot(Guid.NewGuid(), null, "2026-09", slip.EmployeeId);
		var updated = JsonSerializer.Deserialize<SalarySlipLineItem>(JsonSerializer.Serialize(original))!;
		switch (change)
		{
			case "gross": updated.FullMonthAmount = 18000m; break;
			case "type": updated.FormulaType = "FixedAmount"; break;
			case "rate": updated.FormulaValue = 50m; break;
			case "basis": updated.BasisComponentId = Guid.NewGuid(); break;
		}
		var persisted = JsonSerializer.Serialize(new List<SalarySlipLineItem> { updated },
			new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } });
		var restored = PayrollSnapshotComponentParser.BuildSalarySlipLineItems(persisted);
		SalarySlipSnapshotMatcher.SalarySlipFinancialLinesMatchSnapshot(slip, snapshot, restored, [])
			.ShouldBeFalse();
		// Replacing the draft's lines must make the same persisted snapshot current.
		slip.UpdateLineItems(restored, [], DateTime.UtcNow);
		SalarySlipSnapshotMatcher.SalarySlipFinancialLinesMatchSnapshot(slip, snapshot, restored, [])
			.ShouldBeTrue();
	}
}
