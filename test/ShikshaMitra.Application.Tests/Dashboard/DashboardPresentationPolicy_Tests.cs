using ShikshaMitra.Dashboard;
using Shouldly;
using Volo.Abp.Validation;
using Xunit;

namespace ShikshaMitra.Application.Tests.Dashboard;

public class DashboardPresentationPolicy_Tests
{
	[Theory]
	[InlineData("leadership", "teacher")]
	[InlineData("teacher", "parent")]
	[InlineData("studentParent", "hr")]
	[InlineData("financeHr", "admissions")]
	public void Rejects_Role_From_Another_Template(string template, string role) =>
		Should.Throw<AbpValidationException>(() => DashboardPresentationPolicy.Validate(template, role));

	[Theory]
	[InlineData(true, false, "finance")]
	[InlineData(false, true, "hr")]
	[InlineData(true, true, "financeHr")]
	public void Infers_Finance_And_Hr_Presentation_From_Current_Capabilities(bool finance, bool hr, string role) =>
		DashboardPresentationPolicy.ResolveStaff("financeHr", null, [], finance, hr).ShouldBe(role);

	[Fact]
	public void Admission_Role_Changes_Presentation_Within_Existing_Template() =>
		DashboardPresentationPolicy.ResolveStaff("principalAdmin", null, ["Admissions Officer"], false, false).ShouldBe("admissions");

	[Fact]
	public void Requested_Presentation_Does_Not_Grant_Capabilities() =>
		DashboardPresentationPolicy.ResolveStaff("financeHr", "hr", [], false, false).ShouldBe("hr");
}
