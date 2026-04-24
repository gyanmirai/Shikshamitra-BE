using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Academic.Curriculum;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Academic)]
public class ElectiveGroupValidationApi_Tests : ShikshaMitraApiTestBase
{
	public ElectiveGroupValidationApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Return_Validation_Error_With_Fields_For_Missing_Query_Validation()
	{
		var response = await Client.GetAsync("/api/assignment-type");
		response.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Forbidden);
		if (response.StatusCode != HttpStatusCode.BadRequest)
		{
			return;
		}

		var json = await response.Content.ReadAsStringAsync();
		using var document = JsonDocument.Parse(json);
		var root = document.RootElement;

		root.GetProperty("status").GetInt32().ShouldBe((int)HttpStatusCode.BadRequest);
		root.GetProperty("title").GetString().ShouldNotBeNullOrWhiteSpace();
		root.GetProperty("errorCode").GetString().ShouldBe("VALIDATION_ERROR");
		var fields = root.GetProperty("errors");
		fields.TryGetProperty("academicMediumId", out _).ShouldBeTrue();
	}
}
