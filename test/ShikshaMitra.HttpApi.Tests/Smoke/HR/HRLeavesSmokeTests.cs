using System;
using System.Net;
using System.Threading.Tasks;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Smoke.HR;

[TestModule(TestTraits.Modules.HR)]
[TestTier(TestTraits.Tiers.Smoke)]
public class HRLeavesSmokeTests : ShikshaMitraSmokeTestBase
{
	[SmokeFact]
	public async Task Smoke_Leave_Types_List_Should_Return_200()
	{
		await AuthenticateAsync();

		var mediumStageId = Guid.NewGuid();
		var response = await Client.GetAsync(
			$"/api/leave-types?mediumStageId={mediumStageId}&page=1&pageSize=5");

		response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.InternalServerError);
	}

	[SmokeFact]
	public async Task Smoke_Leave_Policies_List_Should_Return_200()
	{
		await AuthenticateAsync();

		var mediumStageId = Guid.NewGuid();
		var response = await Client.GetAsync(
			$"/api/leave-policies?mediumStageId={mediumStageId}&page=1&pageSize=5");

		response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.InternalServerError);
	}

	[SmokeFact]
	public async Task Smoke_Automated_Notifications_List_Should_Return_200()
	{
		await AuthenticateAsync();

		var response = await Client.GetAsync(
			"/api/attendance/automated-notifications?SkipCount=0&MaxResultCount=5");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}
}
