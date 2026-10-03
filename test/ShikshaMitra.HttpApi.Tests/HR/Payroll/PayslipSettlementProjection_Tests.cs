using System;
using System.Text.Json;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.SalarySlips;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class PayslipSettlementProjection_Tests
{
	[Theory]
	[InlineData(10500, 79, 342, 10421, 10842)]
	[InlineData(9500, 72, 309, 9428, 9809)]
	[InlineData(10000, 75, 325, 9925, 10325)]
	public void Signed_source_differences_reconcile_without_proration_or_target_statutory_reassessment(
		decimal earnings, decimal deductions, decimal employer, decimal expectedNet, decimal expectedCost)
	{
		var correction = Correction(earnings, deductions, employer);
		var target = Target();
		PayslipSettlementProjection.Apply(target, correction);
		((decimal)target.NetPay).ShouldBe(expectedNet);
		((decimal)target.EmployerCost).ShouldBe(expectedCost);
		target.GrossSalary.ShouldBe(10000);
		target.EmployeeStateInsuranceWages.ShouldBe(10000);
		target.EarningComponents.ShouldAllBe(x => x.Amount >= 0);
		target.DeductionComponents.ShouldAllBe(x => x.Amount >= 0);
		var recalculated = Target();
		PayslipSettlementProjection.Apply(recalculated, correction);
		recalculated.NetPay.ShouldBe(target.NetPay);
	}
	[Fact]
	public void Source_insurance_cannot_be_recovered_from_unrelated_current_wages()
	{
		Should.Throw<BusinessException>(() => PayslipSettlementProjection.Apply(Target(), Correction(10000, 100, 325)))
			.Data["reason"].ShouldNotBeNull();
	}
	[Fact]
	public void Recovery_rejects_aggregate_deductions_over_half_the_target_wages()
	{
		var error = Should.Throw<BusinessException>(() => PayslipSettlementProjection.Apply(Target(), Correction(4000, 30, 130)));
		error.Code.ShouldBe(HRErrorCodes.PayslipSettlementInvalid);
		error.Data["reason"].ShouldNotBeNull();
	}
	[Fact]
	public void Recovery_does_not_infer_a_legal_wage_base_from_unclassified_earnings()
	{
		var target = Target();
		target.EarningComponents[0].Name = "Custom allowance";
		Should.Throw<BusinessException>(() => PayslipSettlementProjection.Apply(target, Correction(9500, 72, 309)))
			.Data["reason"].ShouldNotBeNull();
	}
	private static RunPayrollItem Target() => new() {
		GrossSalary = 10000, NetPay = 9925, TotalDeduction = 75, EmployerCost = 10325, EmployerContributionTotal = 325,
		EmployeeStateInsuranceWages = 10000,
		EarningComponents = [new() { Name = "Basic", Amount = 10000 }],
		DeductionComponents = [new() { Name = "ESI", Amount = 75 }],
		EmployerContributions = [new() { Name = "ESI", Amount = 325 }] };
	private static PayslipCorrection Correction(decimal earnings, decimal deductions, decimal employer)
	{
		var original = Target();
		var json = JsonSerializer.Serialize(new { original.NetPay, original.TotalDeduction, original.EmployerCost,
			EarningComponentsJson = JsonSerializer.Serialize(original.EarningComponents),
			DeductionComponentsJson = JsonSerializer.Serialize(original.DeductionComponents),
			EmployerContributionsJson = JsonSerializer.Serialize(original.EmployerContributions) });
		var correction = new PayslipCorrection(Guid.NewGuid(), new PayslipDocument(Guid.NewGuid(), Guid.NewGuid(),
			Guid.NewGuid(), Guid.NewGuid(), "2026-03", 1, DateTime.UtcNow, Guid.NewGuid(), "{}", [1]), Guid.NewGuid(),
			"Synthetic settlement", Guid.NewGuid(), json);
		var candidate = Target();
		candidate.EarningComponents[0].Amount = (double)earnings;
		candidate.DeductionComponents[0].Amount = (double)deductions;
		candidate.EmployerContributions[0].Amount = (double)employer;
		candidate.NetPay = (double)(earnings - deductions);
		candidate.TotalDeduction = (double)deductions;
		candidate.EmployerCost = (double)(earnings + employer);
		correction.Calculate("[]", JsonSerializer.Serialize(candidate), Guid.NewGuid());
		return correction;
	}
}
