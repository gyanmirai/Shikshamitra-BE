using System;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.Controllers.HR.Payroll.SalarySlips;
using ShikshaMitra.HR.Payroll.SalarySlips;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class SalarySlipResponseMapping_Tests
{
	[Fact]
	public async Task Response_preserves_finalized_esi_and_compensation_evidence()
	{
		var dto = new SalarySlipDto
		{
			EmployeeId = Guid.NewGuid(),
			EmployeeStateInsuranceRuleId = Guid.NewGuid(),
			EmployeeStateInsuranceRuleVersion = 1,
			EmployeeStateInsuranceWages = 18000m,
			EmployeeStateInsuranceGovernmentContribution = 0m,
			EmployeeStateInsuranceCoverageDecision = "WITHIN_WAGE_CEILING",
			EmployeeStateInsuranceNumber = "9990000913",
			EmployeeStateInsuranceContributionPeriodStart = new DateTime(2026, 4, 1),
			EmployeeStateInsuranceContributionPeriodEnd = new DateTime(2026, 9, 30),
			CompensationRevisionId = Guid.NewGuid(),
			CompensationRevisionNumber = 1,
			CompensationSalaryBasis = "Gross",
			CompensationProtectionPolicy = "ProtectGross",
			CompensationQuotedAmount = 18000m,
			CompensationProtectedAmount = 18000m,
			CompensationNormalizedProtectedAmount = 18000m,
			CompensationTargetDifference = 0m
		};
		var service = Substitute.For<ISalarySlipAppService>();
		service.GetSalarySlipAsync(dto.EmployeeId, Arg.Any<string>()).Returns(dto);
		var response = await new SalarySlipController(service).GetSalarySlipAsync(dto.EmployeeId, "2026-06");
		response.EmployeeStateInsuranceRuleId.ShouldBe(dto.EmployeeStateInsuranceRuleId);
		response.EmployeeStateInsuranceRuleVersion.ShouldBe(dto.EmployeeStateInsuranceRuleVersion);
		response.EmployeeStateInsuranceWages.ShouldBe(dto.EmployeeStateInsuranceWages);
		response.EmployeeStateInsuranceGovernmentContribution.ShouldBe(dto.EmployeeStateInsuranceGovernmentContribution);
		response.EmployeeStateInsuranceCoverageDecision.ShouldBe(dto.EmployeeStateInsuranceCoverageDecision);
		response.EmployeeStateInsuranceNumber.ShouldBe(dto.EmployeeStateInsuranceNumber);
		response.EmployeeStateInsuranceContributionPeriodStart.ShouldBe(dto.EmployeeStateInsuranceContributionPeriodStart);
		response.EmployeeStateInsuranceContributionPeriodEnd.ShouldBe(dto.EmployeeStateInsuranceContributionPeriodEnd);
		response.CompensationRevisionId.ShouldBe(dto.CompensationRevisionId);
		response.CompensationRevisionNumber.ShouldBe(dto.CompensationRevisionNumber);
		response.CompensationSalaryBasis.ShouldBe(dto.CompensationSalaryBasis);
		response.CompensationProtectionPolicy.ShouldBe(dto.CompensationProtectionPolicy);
		response.CompensationQuotedAmount.ShouldBe(dto.CompensationQuotedAmount);
		response.CompensationProtectedAmount.ShouldBe(dto.CompensationProtectedAmount);
		response.CompensationNormalizedProtectedAmount.ShouldBe(dto.CompensationNormalizedProtectedAmount);
		response.CompensationTargetDifference.ShouldBe(dto.CompensationTargetDifference);
	}

	[Fact]
	public async Task Response_does_not_fabricate_missing_snapshot_references()
	{
		var dto = new SalarySlipDto { EmployeeId = Guid.NewGuid() };
		var service = Substitute.For<ISalarySlipAppService>();
		service.GetSalarySlipAsync(dto.EmployeeId, Arg.Any<string>()).Returns(dto);
		var response = await new SalarySlipController(service).GetSalarySlipAsync(dto.EmployeeId, "2026-06");
		response.EmployeeStateInsuranceRuleId.ShouldBeNull();
		response.EmployeeStateInsuranceNumber.ShouldBeNull();
		response.CompensationRevisionId.ShouldBeNull();
		response.EmployeeStateInsuranceWages.ShouldBe(0m);
	}
}
