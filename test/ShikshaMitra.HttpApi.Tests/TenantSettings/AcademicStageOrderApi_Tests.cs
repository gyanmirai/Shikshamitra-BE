using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Xunit;

namespace ShikshaMitra.TenantSettings;

[Trait("Module", "TenantSettings"), Trait("Tier", "Critical")]
public class AcademicStageOrderApi_Tests(AcademicSetupValidationFactory factory) : IClassFixture<AcademicSetupValidationFactory>
{
	[Fact]
	public async Task Stage_reorder_route_saves_order_in_hierarchy_and_medium_and_new_stage_appends()
	{
		using var client = factory.CreateClient();
		var hierarchy = await CreateHierarchyAsync(client);
		var expected = hierarchy.StageIds.Reverse().ToArray();
		var response = await client.PutAsJsonAsync(Route(hierarchy.MediumId), new { ids = expected });
		response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
		(await ReadMediumFromHierarchyAsync(client, hierarchy.MediumId)).EducationMediumStages.Select(x => x.Id).ShouldBe(expected);
		(await client.GetFromJsonAsync<EducationMediumDto>($"/api/app/education-medium/{hierarchy.MediumId}"))!
			.EducationMediumStages.Select(x => x.Id).ShouldBe(expected);
		var created = await client.PostAsJsonAsync("/api/app/education-medium-stage", new { name = "Middle", educationMediumId = hierarchy.MediumId });
		created.EnsureSuccessStatusCode();
		var stage = (await created.Content.ReadFromJsonAsync<EducationMediumStageDto>())!;
		(await ReadMediumFromHierarchyAsync(client, hierarchy.MediumId)).EducationMediumStages.Select(x => x.Id).ShouldBe(expected.Append(stage.Id));
	}

	[Theory]
	[InlineData("duplicate")]
	[InlineData("partial")]
	[InlineData("unknown")]
	[InlineData("other-parent")]
	public async Task Invalid_stage_reorder_returns_bad_request_without_changing_saved_order(string invalidKind)
	{
		using var client = factory.CreateClient();
		var hierarchy = await CreateHierarchyAsync(client);
		var expected = hierarchy.StageIds.Reverse().ToArray();
		(await client.PutAsJsonAsync(Route(hierarchy.MediumId), new { ids = expected })).EnsureSuccessStatusCode();
		var foreignId = invalidKind == "other-parent" ? (await CreateHierarchyAsync(client)).StageIds[0] : Guid.NewGuid();
		Guid[] ids = invalidKind switch
		{
			"duplicate" => [hierarchy.StageIds[0], hierarchy.StageIds[0]],
			"partial" => [hierarchy.StageIds[0]],
			_ => [hierarchy.StageIds[0], foreignId]
		};
		var response = await client.PutAsJsonAsync(Route(hierarchy.MediumId), new { ids });
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
		(await ReadMediumFromHierarchyAsync(client, hierarchy.MediumId)).EducationMediumStages.Select(x => x.Id).ShouldBe(expected);
	}

	[Fact]
	public async Task View_permission_cannot_reorder_stages()
	{
		using var client = factory.CreateClient();
		var hierarchy = await CreateHierarchyAsync(client);
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, ShikshaMitraSubmodulePermissions.InstituteSetup.AcademicSetup.View);
		var response = await client.PutAsJsonAsync(Route(hierarchy.MediumId), new { ids = hierarchy.StageIds.Reverse().ToArray() });
		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
		(await ReadMediumFromHierarchyAsync(client, hierarchy.MediumId)).EducationMediumStages.Select(x => x.Id).ShouldBe(hierarchy.StageIds);
	}

	[Fact]
	public async Task Host_request_cannot_reorder_tenant_stages()
	{
		using var client = factory.CreateClient();
		var hierarchy = await CreateHierarchyAsync(client);
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, "host");
		var response = await client.PutAsJsonAsync(Route(hierarchy.MediumId), new { ids = hierarchy.StageIds.Reverse().ToArray() });
		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
		client.DefaultRequestHeaders.Remove(TestAuthHandler.TestTenantIdHeader);
		(await ReadMediumFromHierarchyAsync(client, hierarchy.MediumId)).EducationMediumStages.Select(x => x.Id).ShouldBe(hierarchy.StageIds);
	}

	private async Task<Hierarchy> CreateHierarchyAsync(HttpClient client)
	{
		var firstId = await factory.SeedStageAsync();
		var first = (await client.GetFromJsonAsync<EducationMediumStageDto>($"/api/app/education-medium-stage/{firstId}"))!;
		var response = await client.PostAsJsonAsync("/api/app/education-medium-stage", new { name = "Secondary", educationMediumId = first.EducationMediumId });
		response.EnsureSuccessStatusCode();
		var second = (await response.Content.ReadFromJsonAsync<EducationMediumStageDto>())!;
		return new(first.EducationMediumId, [first.Id, second.Id]);
	}

	private static async Task<EducationMediumDto> ReadMediumFromHierarchyAsync(HttpClient client, Guid mediumId)
	{
		var boards = (await client.GetFromJsonAsync<PagedResultDto<EducationBoardDto>>("/api/app/education-board?MaxResultCount=1000"))!;
		return boards.Items.SelectMany(x => x.EducationMediums).Single(x => x.Id == mediumId);
	}

	private static string Route(Guid mediumId) => $"/api/education-mediums/{mediumId}/stages/order";
	private sealed record Hierarchy(Guid MediumId, Guid[] StageIds);
}
