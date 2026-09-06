using Shouldly;
using Xunit;

namespace ShikshaMitra.Seeders;

public class PayrollStatutorySettingsBackfillSeederContributor_Tests
{
	[Fact]
	public void Should_have_a_v2_step_before_the_salary_template_backfill()
	{
		PayrollStatutorySettingsBackfillSeederContributor.StepName
			.ShouldBe("PayrollStatutorySettingsBackfillSeeder");
		PayrollStatutorySettingsBackfillSeederContributor.StepVersion
			.ShouldBe("PayrollStatutorySettingsBackfillSeeder:v2");
		PayrollStatutorySettingsBackfillSeederContributor.StepOrder.ShouldBe(161);
		PayrollStatutorySettingsBackfillSeederContributor.StepOrder
			.ShouldBeLessThan(SalaryTemplateBackfillSeederContributor.StepOrder);
	}
}
