using Shouldly;
using Xunit;

namespace ShikshaMitra.Seeders;

public class PayrollStatutorySettingsBackfillSeederContributor_Tests
{
	[Fact]
	public void Should_have_a_dedicated_versioned_step_before_the_salary_template_backfill()
	{
		PayrollStatutorySettingsBackfillSeederContributor.StepName
			.ShouldBe("PayrollStatutorySettingsBackfillSeeder");
		PayrollStatutorySettingsBackfillSeederContributor.StepVersion
			.ShouldBe("PayrollStatutorySettingsBackfillSeeder:v1");
		PayrollStatutorySettingsBackfillSeederContributor.StepOrder.ShouldBe(161);
		PayrollStatutorySettingsBackfillSeederContributor.StepOrder
			.ShouldBeLessThan(SalaryTemplateBackfillSeederContributor.StepOrder);
	}
}
