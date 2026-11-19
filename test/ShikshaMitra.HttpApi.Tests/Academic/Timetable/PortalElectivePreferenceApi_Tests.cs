using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Common.Middleware;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Academic.Timetable.Portal;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Academic)]
public class PortalElectivePreferenceApi_Tests(ShikshaMitraApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	private const string Route = "/api/mobile/v1/academics/timetable/elective-preferences";

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Anonymous_preferences_requests_require_authentication(bool submit)
	{
		using var client = CreateAnonymousClient();
		using var response = submit ? await client.PostAsJsonAsync(Route, new PortalElectivePreferenceSubmitInput()) : await client.GetAsync(Route);
		response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Malformed_query_and_empty_submission_return_bad_request()
	{
		using var malformed = await Client.GetAsync(Route + "?studentId=invalid&academicYearId=invalid");
		malformed.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		using var empty = await Client.PostAsJsonAsync(Route, new PortalElectivePreferenceSubmitInput());
		empty.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public void Conflict_mapping_preserves_HTTP_409()
	{
		ApiProblemDetailsMapper.ResolveBusinessStatusCode(ElectivePreferenceErrorCodes.Conflict).ShouldBe(409);
		ApiProblemDetailsMapper.NormalizeStatusCode(400, ElectivePreferenceErrorCodes.Conflict).ShouldBe(409);
	}
}
