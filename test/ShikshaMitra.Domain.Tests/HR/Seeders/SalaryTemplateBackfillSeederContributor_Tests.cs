using Shouldly;
using Xunit;

namespace ShikshaMitra.Seeders;

public class SalaryTemplateBackfillSeederContributor_Tests
{
	[Fact]
	public void Should_have_a_dedicated_versioned_step_after_the_HR_catalog()
	{
		SalaryTemplateBackfillSeederContributor.StepName.ShouldBe("SalaryTemplateBackfillSeeder");
		SalaryTemplateBackfillSeederContributor.StepVersion.ShouldBe("SalaryTemplateBackfillSeeder:v3");
		SalaryTemplateBackfillSeederContributor.StepOrder.ShouldBe(162);
	}
}
