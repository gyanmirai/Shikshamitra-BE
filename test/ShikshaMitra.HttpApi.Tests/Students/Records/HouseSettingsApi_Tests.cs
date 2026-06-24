using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Students.Records.Settings;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.Records;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Students)]
public class HouseSettingsApi_Tests : ShikshaMitraApiTestBase
{
	public HouseSettingsApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Reject_Duplicate_House_Name_For_Academic_Medium()
	{
		var houseName = $"Sapphire {System.Guid.NewGuid():N}".Substring(0, 18);
		var createUrl = $"/api/students/records/academic-mediums/{Factory.SeededMediumId}/house-settings";

		var firstResponse = await Client.PostAsJsonAsync(
			createUrl,
			new CreateUpdateHouseSettingsDto
			{
				Name = houseName,
				Color = "#0EA5E9"
			},
			JsonOptions);

		firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

		var duplicateResponse = await Client.PostAsJsonAsync(
			createUrl,
			new CreateUpdateHouseSettingsDto
			{
				Name = $"  {houseName.ToLowerInvariant()}  ",
				Color = "#0284C7"
			},
			JsonOptions);

		duplicateResponse.StatusCode.ShouldNotBe(HttpStatusCode.OK);
		var duplicateContent = await duplicateResponse.Content.ReadAsStringAsync();
		using var duplicateDocument = JsonDocument.Parse(duplicateContent);
		duplicateDocument.RootElement.GetProperty("errorCode").GetString()
			.ShouldBe(StudentErrorCodes.HouseSettingsAlreadyExists);
		var duplicateDetail = duplicateDocument.RootElement.GetProperty("detail").GetString();
		duplicateDetail.ShouldNotBeNullOrWhiteSpace();
		duplicateDetail.ShouldContain("already exists");
		duplicateDetail.ShouldNotContain("One or more business rules were violated");

		var listResponse = await Client.GetAsync(createUrl);
		listResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await listResponse.Content
			.ReadFromJsonAsync<Result<List<HouseSettingsDto>>>(JsonOptions);

		body.ShouldNotBeNull();
		body.Data.ShouldNotBeNull();
		body.Data.Count(house => house.Name == houseName).ShouldBe(1);
	}
}
