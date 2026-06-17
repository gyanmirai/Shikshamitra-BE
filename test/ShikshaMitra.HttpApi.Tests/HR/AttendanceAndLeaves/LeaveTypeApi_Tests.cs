using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Infrastructure.Data;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class LeaveTypeApi_Tests : ShikshaMitraApiTestBase
{
	private const string BaseUrl = "/api/hr-leave-types";

	public LeaveTypeApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory)
	{
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Hr_Leave_Type_Endpoint_Should_Not_Exist()
	{
		var getResponse = await Client.GetAsync($"{BaseUrl}?page=1&pageSize=10");
		getResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);

		var postResponse = await Client.PostAsJsonAsync(BaseUrl, new { name = "Sick", color = "#28A745" }, JsonOptions);
		postResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}
}
