using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.Attendance;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Students)]
public class AttendanceSettingsAuthorizationApi_Tests(ShikshaMitraApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	[Fact, TestTier(TestTraits.Tiers.Critical)]
	public async Task Removed_configuration_routes_do_not_accept_requests()
	{
		(await Client.GetAsync("/api/students/attendance/settings")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
		(await Client.PutAsJsonAsync("/api/students/attendance/settings/draft", new { })).StatusCode.ShouldBe(HttpStatusCode.NotFound);
		(await Client.PostAsJsonAsync("/api/students/attendance/settings/preview", new { })).StatusCode.ShouldBe(HttpStatusCode.NotFound);
		(await Client.PostAsJsonAsync("/api/students/attendance/settings/publish", new { })).StatusCode.ShouldBe(HttpStatusCode.NotFound);
		(await Client.GetAsync("/api/students/attendance/schedules?enrollmentId=00000000-0000-0000-0000-000000000001")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
		(await Client.GetAsync("/api/students/attendance/enrollments")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}
}
