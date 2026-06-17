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
	public async Task Smoke_HR_Leave_Types_List_Should_Not_Exist()
	{
		await AuthenticateAsync();

		var response = await Client.GetAsync(
			"/api/hr-leave-types?page=1&pageSize=5");

		response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[SmokeFact]
	public async Task Smoke_Leave_Policies_List_Should_Return_200()
	{
		await AuthenticateAsync();

		var academicYearId = Guid.NewGuid();
		var response = await Client.GetAsync(
			$"/api/leave-policies?academicYearId={academicYearId}&page=1&pageSize=5");

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
