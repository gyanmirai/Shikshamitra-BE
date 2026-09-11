using System;
using System.Globalization;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Eligibility_source_fingerprint_is_independent_of_staff_calendar_culture() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateEligibilityFixtureAsync(EligibilityPolicyJson([EligibilityAgeCriterion()]), new DateTime(2021, 4, 1));
		var previous = CultureInfo.CurrentCulture;
		try
		{
			CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-IN");
			var evaluated = await EvaluateEligibilityAsync(fixture);
			evaluated.Status.ShouldBe("eligible");
			evaluated.LatestEvaluation!.Criteria[0].Actual.ShouldBe("60 completed months; DOB 2021-04-01");
			CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
			var state = await GetRequiredService<IAdmissionEligibilityAppService>().GetAsync(fixture.ApplicationId, fixture.StageId);
			state.IsStale.ShouldBeFalse();
			state.CanComplete.ShouldBeTrue();
		}
		finally { CultureInfo.CurrentCulture = previous; }
	});
}
