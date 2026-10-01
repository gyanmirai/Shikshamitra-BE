using System.Net;
using System.Threading.Tasks;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Academic.Timetable.Portal;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Academic)]
public class PortalTimetableApi_Tests(ShikshaMitraApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
    [Theory]
    [InlineData("context")]
    [InlineData("schedule?academicYearId=81000000-0000-0000-0000-000000000001&fromDate=2026-10-01&toDate=2026-10-01")]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Anonymous_requests_are_denied(string path)
    {
        using var client = CreateAnonymousClient();
        (await client.GetAsync("/api/mobile/v1/academics/timetable/" + path)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Staff_wildcard_scope_does_not_grant_access_to_student_portal_data()
    {
        (await Client.GetAsync("/api/mobile/v1/academics/timetable/context")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("context?studentId=invalid")]
    [InlineData("schedule?academicYearId=81000000-0000-0000-0000-000000000001&fromDate=invalid&toDate=2026-10-01")]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Malformed_query_fields_are_rejected_by_HTTP_binding(string path)
    {
        (await Client.GetAsync("/api/mobile/v1/academics/timetable/" + path)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
