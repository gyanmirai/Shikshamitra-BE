using System;
using ShikshaMitra.Domain.Finance.Policies;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance.Policies;

public class ProrationPolicyValidation_Tests
{
	[Theory]
	[InlineData(true, false)]
	[InlineData(false, true)]
	[InlineData(true, true)]
	public void Full_Month_Charge_Qualifies_For_An_Enabled_Event(bool admission, bool withdrawal)
	{
		var input = Policy(new ProrationPolicyRuleDto
		{
			FeeHeadId = Guid.NewGuid(),
			RuleType = "FullMonthCharge",
			ApplyOnAdmission = admission,
			ApplyOnEarlyWithdrawal = withdrawal,
			EarlyWithdrawalRuleType = "FullMonthCharge"
		});

		new CreateUpdateProrationPolicyDtoValidator().Validate(input).IsValid.ShouldBeTrue();
	}

	[Fact]
	public void Withdrawal_Only_Legacy_Full_Month_Charge_Qualifies()
	{
		var input = Policy(new ProrationPolicyRuleDto
		{
			FeeHeadId = Guid.NewGuid(),
			RuleType = "FullMonthCharge",
			ApplyOnAdmission = false,
			ApplyOnEarlyWithdrawal = true
		});

		new CreateUpdateProrationPolicyDtoValidator().Validate(input).IsValid.ShouldBeTrue();
	}

	[Theory]
	[InlineData("FullCharge", true, true)]
	[InlineData("FullMonthCharge", false, false)]
	public void No_Proration_Or_Disabled_Events_Do_Not_Qualify(string model, bool admission, bool withdrawal)
	{
		var input = Policy(new ProrationPolicyRuleDto
		{
			FeeHeadId = Guid.NewGuid(),
			RuleType = model,
			ApplyOnAdmission = admission,
			ApplyOnEarlyWithdrawal = withdrawal,
			EarlyWithdrawalRuleType = model
		});

		new CreateUpdateProrationPolicyDtoValidator().Validate(input).Errors
			.ShouldContain(error => error.PropertyName == "FeeHeadRules" &&
				error.ErrorMessage.Contains("At least one Fee Head"));
	}

	private static CreateUpdateProrationPolicyDto Policy(ProrationPolicyRuleDto rule) => new()
	{
		AcademicYearId = Guid.NewGuid(),
		PolicyName = "Monthly proration policy",
		FeeHeadRules = [rule]
	};
}
