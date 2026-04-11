using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Smoke.HR;

[TestModule(TestTraits.Modules.HR)]
[TestTier(TestTraits.Tiers.Smoke)]
public class HREmployeeSmokeTests : ShikshaMitraSmokeTestBase
{
	[SmokeFact]
	public async Task Smoke_Employee_List_Should_Return_200()
	{
		await AuthenticateAsync();

		var response = await Client.GetAsync("/api/employees?page=1&pageSize=5");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<EmployeeListResponse>();
		body.ShouldNotBeNull();
		body.Items.ShouldNotBeNull();
	}

	[SmokeFact]
	public async Task Smoke_Document_Config_List_Should_Return_200()
	{
		await AuthenticateAsync();

		var response = await Client.GetAsync("/api/employees/documents?page=1&pageSize=5");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}
}
