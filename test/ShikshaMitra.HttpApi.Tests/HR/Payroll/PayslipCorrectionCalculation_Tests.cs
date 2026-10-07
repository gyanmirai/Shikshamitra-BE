using System;
using System.Collections.Generic;
using System.Text.Json;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.SalarySlips;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class PayslipCorrectionCalculation_Tests
{
	[Fact]
	public void Zero_pay_evidence_remains_equal_after_jsonb_removes_negative_zero()
	{
		var persisted = new RunPayrollItem { NetPay = 0, EmployerCost = 0 };
		var recalculated = new RunPayrollItem
		{
			NetPay = BitConverter.Int64BitsToDouble(long.MinValue),
			EmployerCost = BitConverter.Int64BitsToDouble(long.MinValue)
		};
		JsonSerializer.Serialize(persisted).ShouldNotBe(JsonSerializer.Serialize(recalculated));
		Should.NotThrow(() => PayslipCorrectionAppService.EnsureSameCalculation(persisted, recalculated));
	}

	[Fact]
	public void Equivalent_component_amount_representations_remain_equal()
	{
		var persisted = new RunPayrollItem { EarningComponents = [new() { Name = "Basic", Amount = 0, FullMonthAmount = 10000.00m }] };
		var recalculated = new RunPayrollItem
		{
			EarningComponents = [new() { Name = "Basic",
			Amount = BitConverter.Int64BitsToDouble(long.MinValue), FullMonthAmount = 10000m }]
		};
		JsonSerializer.Serialize(persisted).ShouldNotBe(JsonSerializer.Serialize(recalculated));
		Should.NotThrow(() => PayslipCorrectionAppService.EnsureSameCalculation(persisted, recalculated));
	}

	[Theory]
	[InlineData("net")]
	[InlineData("employer_cost")]
	[InlineData("component")]
	[InlineData("insurance")]
	[InlineData("coverage")]
	[InlineData("configuration")]
	[InlineData("issues")]
	[InlineData("component_order")]
	[InlineData("source")]
	[InlineData("null_string")]
	public void Changed_calculation_evidence_is_still_rejected_exactly(string changedField)
	{
		var persisted = new RunPayrollItem
		{
			EarningComponents = [new() { Name = "Basic", Amount = 100 }, new() { Name = "Allowance", Amount = 20 }],
			EmployeeStateInsuranceNumber = "1234567890",
			EmployeeStateInsuranceCoverageDecision = "WITHIN_WAGE_CEILING",
			EsiConfigurationId = Guid.NewGuid(),
			IssueCodes = new List<string> { IssueCodes.EsiInsuranceNumberRequired }
		};
		var recalculated = JsonSerializer.Deserialize<RunPayrollItem>(JsonSerializer.Serialize(persisted))!;
		switch (changedField)
		{
			case "net": recalculated.NetPay = 0.000000001; break;
			case "employer_cost": recalculated.EmployerCost = 0.01; break;
			case "component": recalculated.EarningComponents[0].Amount += 0.000000001; break;
			case "insurance": recalculated.EmployeeStateInsuranceNumber = "0987654321"; break;
			case "coverage": recalculated.EmployeeStateInsuranceCoverageDecision = "ABOVE_WAGE_CEILING"; break;
			case "configuration": recalculated.EsiConfigurationId = Guid.NewGuid(); break;
			case "issues": recalculated.IssueCodes.Clear(); break;
			case "component_order": recalculated.EarningComponents.Reverse(); break;
			case "source": recalculated.PayrollAttendanceSource = "payslip_correction"; break;
			case "null_string": recalculated.CompensationSalaryBasis = ""; break;
			default: throw new ArgumentOutOfRangeException(nameof(changedField));
		}
		Should.Throw<BusinessException>(() => PayslipCorrectionAppService.EnsureSameCalculation(persisted, recalculated))
			.Code.ShouldBe(HRErrorCodes.PayslipConflict);
	}
}