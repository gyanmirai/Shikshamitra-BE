using System;
using System.Collections.Generic;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.SalarySlips;
using ShikshaMitra.HR.Payroll.Services;
using ShikshaMitra.HR.Payroll.Settings;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class PayrollCalculationRegression_Tests
{
	[Theory]
	[InlineData("ProvidentFundRuleId")]
	[InlineData("ProvidentFundRuleVersion")]
	[InlineData("ProfessionalTaxRuleId")]
	[InlineData("ProfessionalTaxRuleVersion")]
	[InlineData("EmployeeStateInsuranceRuleId")]
	[InlineData("EmployeeStateInsuranceRuleVersion")]
	[InlineData("EmployeeStateInsuranceWages")]
	[InlineData("EmployeeStateInsuranceGovernmentContribution")]
	[InlineData("EmployeeStateInsuranceCoverageDecision")]
	[InlineData("EmployeeStateInsuranceNumber")]
	[InlineData("EmployeeStateInsuranceContributionPeriodStart")]
	[InlineData("EmployeeStateInsuranceContributionPeriodEnd")]
	[InlineData("EsiConfigurationId")]
	[InlineData("CompensationRevisionId")]
	[InlineData("CompensationRevisionNumber")]
	[InlineData("CompensationSalaryBasis")]
	[InlineData("CompensationProtectionPolicy")]
	[InlineData("CompensationQuotedAmount")]
	[InlineData("CompensationProtectedAmount")]
	[InlineData("CompensationNormalizedProtectedAmount")]
	[InlineData("CompensationTargetDifference")]
	public void Every_copied_statutory_and_compensation_field_invalidates_draft(string field)
	{
		var slip = new SalarySlip(Guid.NewGuid(), null, Guid.NewGuid(), "2026-04", [], [], DateTime.UtcNow);
		var snapshot = new PayrollRunItemSnapshot(Guid.NewGuid(), null, "2026-04", slip.EmployeeId)
		{
			DayCalculationMode = "working-days"
		};
		SalarySlipGenerationRules.ApplySnapshotMetadata(slip, snapshot, DayCalculationType.WorkingDays);
		SalarySlipSnapshotMatcher.SalarySlipMatchesSnapshot(slip, snapshot, [], [], []).ShouldBeTrue();
		var property = typeof(PayrollRunItemSnapshot).GetProperty(field)!;
		var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
		object value = type == typeof(Guid) ? Guid.NewGuid() :
			type == typeof(int) ? 2 :
			type == typeof(decimal) ? 123m :
			type == typeof(DateTime) ? new DateTime(2026, 4, 1) : "changed evidence";
		property.SetValue(snapshot, value);
		SalarySlipSnapshotMatcher.SalarySlipMatchesSnapshot(slip, snapshot, [], [], []).ShouldBeFalse();
	}

	[Fact]
	public void Zero_earned_basic_should_not_switch_pf_to_a_one_time_payment()
	{
		var employeeId = Guid.NewGuid();
		var basic = new EmployeePayrollComponent(Guid.NewGuid(), employeeId, "Basic", null, null,
			10000m, true, false, null, SalaryComponentType.Fixed, SalaryComponentDependency.None, true);
		var bonus = new EmployeePayrollComponent(Guid.NewGuid(), employeeId, "One time bonus", null, null,
			1000m, true, true, new DateTime(2026, 4, 1), SalaryComponentType.Fixed, SalaryComponentDependency.None, true);
		var pf = new EmployeePayrollComponent(Guid.NewGuid(), employeeId, "PF", null, null,
			0m, true, false, null, SalaryComponentType.Fixed, SalaryComponentDependency.None, false,
			semanticType: ShikshaMitra.HR.Payroll.Templates.PayrollComponentSemanticType.EmployeeProvidentFund);
		var rule = new ProvidentFund(Guid.NewGuid(), null, "TEST", "Basic PF", 12m, 12m, PfContributionBasis.Basic);
		var result = PayrollComputation.Calculate(0, 0, 0m, 26m, [basic, bonus, pf], new(rule, []));
		result.BasicSalary.ShouldBe(0m);
		result.TotalDeduction.ShouldBe(0m);
		result.NetPay.ShouldBe(1000m);
	}

	[Theory]
	[InlineData(1, 0.5, 0, 1)]
	[InlineData(1, 0, 0.5, 0.5)]
	[InlineData(1, 1, 0, 1)]
	[InlineData(1, 0.5, 0.5, 0.5)]
	[InlineData(0.5, 0.5, 0, 0.5)]
	public void Half_day_attendance_and_approved_leave_respect_day_capacity(decimal capacity, decimal paid, decimal unpaid, decimal expected)
	{
		var date = new DateOnly(2026, 4, 1);
		var basis = new PayrollBasisDaysResult(capacity, DayCalculationType.WorkingDays, false,
			WorkingUnitsByDate: new Dictionary<DateOnly, decimal> { [date] = capacity });
		var attendance = new EmployeeAttendance { AttendanceDate = date,
			StatusType = EmployeeAttendanceStatusType.HalfDayLeave };
		var leaves = new Dictionary<DateOnly, PayrollLeaveUnits> { [date] = new(paid, unpaid) };
		PayrollAttendanceCalculator.CalculatePayableDays([attendance], basis, 2026, 4, [], leaves)
			.PayableDays.ShouldBe(expected);
		PayrollAttendanceCalculator.CalculatePayableDays(
			new List<PayrollApprovedAttendanceDay> { new(date, EmployeeAttendanceStatusType.HalfDayLeave, capacity * 0.5m) },
			basis, 2026, 4, [], leaves).PayableDays.ShouldBe(expected);
	}

	[Theory]
	[InlineData("insurance-number")]
	[InlineData("employer-contribution")]
	[InlineData("compensation-revision")]
	public void Draft_should_refresh_when_non_employee_deduction_evidence_changes(string change)
	{
		var slip = new SalarySlip(Guid.NewGuid(), null, Guid.NewGuid(), "2026-04",
			[new SalarySlipLineItem("Basic", 10000m)], [], DateTime.UtcNow);
		var snapshot = new PayrollRunItemSnapshot(Guid.NewGuid(), null, "2026-04", slip.EmployeeId)
		{
			BasisDays = 1m, PayableDays = 1m, ProrationFactor = 1m,
			DayCalculationMode = "working-days", GrossSalary = 10000m, NetPay = 10000m
		};
		SalarySlipGenerationRules.ApplySnapshotMetadata(slip, snapshot, DayCalculationType.WorkingDays);
		switch (change)
		{
			case "insurance-number":
				snapshot.EmployeeStateInsuranceNumber = "NEW-INSURANCE-NUMBER";
				break;
			case "employer-contribution":
				snapshot.EmployerContributionTotal = 325m;
				snapshot.EmployerContributionsJson = "[{\"Name\":\"Employer ESI\",\"Amount\":325}]";
				break;
			case "compensation-revision":
				snapshot.CompensationRevisionId = Guid.NewGuid();
				snapshot.CompensationRevisionNumber = 2;
				break;
		}
		SalarySlipSnapshotMatcher.SalarySlipMatchesSnapshot(slip, snapshot, slip.Earnings, slip.Deductions,
			PayrollSnapshotComponentParser.BuildSalarySlipLineItems(snapshot.EmployerContributionsJson))
			.ShouldBeFalse();
	}
}
