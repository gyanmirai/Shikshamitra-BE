using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.TenantSettings.AcademicSetups.Divisions;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Xunit;

namespace ShikshaMitra.TenantSettings;

[Trait("Module", "TenantSettings"), Trait("Tier", "Critical")]
public class AcademicOrderApi_Tests(AcademicSetupValidationFactory factory) : IClassFixture<AcademicSetupValidationFactory>
{
	[Fact]
	public async Task Reorder_routes_persist_class_and_division_order_in_the_returned_hierarchy()
	{
		using var client = factory.CreateClient();
		var hierarchy = await CreateHierarchyAsync(client);
		var classOrder = hierarchy.ClassIds.Reverse().ToArray();
		var divisionOrder = hierarchy.DivisionIds.Reverse().ToArray();
		var classes = await client.PutAsJsonAsync(ClassOrderRoute(hierarchy.StageId), new { ids = classOrder });
		classes.StatusCode.ShouldBe(HttpStatusCode.NoContent, await classes.Content.ReadAsStringAsync());
		var divisions = await client.PutAsJsonAsync(DivisionOrderRoute(hierarchy.ClassIds[0]), new { ids = divisionOrder });
		divisions.StatusCode.ShouldBe(HttpStatusCode.NoContent, await divisions.Content.ReadAsStringAsync());
		var stage = await ReadStageAsync(client, hierarchy.StageId);
		stage.Standards.Select(x => x.Id).ShouldBe(classOrder);
		stage.Standards.Single(x => x.Id == hierarchy.ClassIds[0]).Divisions.Select(x => x.Id).ShouldBe(divisionOrder);
		var standard = (await client.GetFromJsonAsync<StandardDto>($"/api/app/standard/{hierarchy.ClassIds[0]}"))!;
		standard.Divisions.Select(x => x.Id).ShouldBe(divisionOrder);
	}

	[Theory]
	[InlineData("duplicate")]
	[InlineData("partial")]
	[InlineData("unknown")]
	public async Task Invalid_reorder_payloads_return_bad_request_without_changing_saved_order(string invalidKind)
	{
		using var client = factory.CreateClient();
		var hierarchy = await CreateHierarchyAsync(client);
		var classOrder = hierarchy.ClassIds.Reverse().ToArray();
		var divisionOrder = hierarchy.DivisionIds.Reverse().ToArray();
		(await client.PutAsJsonAsync(ClassOrderRoute(hierarchy.StageId), new { ids = classOrder })).EnsureSuccessStatusCode();
		(await client.PutAsJsonAsync(DivisionOrderRoute(hierarchy.ClassIds[0]), new { ids = divisionOrder })).EnsureSuccessStatusCode();
		var invalidClasses = await client.PutAsJsonAsync(ClassOrderRoute(hierarchy.StageId), new { ids = InvalidIds(hierarchy.ClassIds, invalidKind) });
		invalidClasses.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await invalidClasses.Content.ReadAsStringAsync());
		var invalidDivisions = await client.PutAsJsonAsync(DivisionOrderRoute(hierarchy.ClassIds[0]), new { ids = InvalidIds(hierarchy.DivisionIds, invalidKind) });
		invalidDivisions.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await invalidDivisions.Content.ReadAsStringAsync());
		var stage = await ReadStageAsync(client, hierarchy.StageId);
		stage.Standards.Select(x => x.Id).ShouldBe(classOrder);
		stage.Standards.Single(x => x.Id == hierarchy.ClassIds[0]).Divisions.Select(x => x.Id).ShouldBe(divisionOrder);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task View_permission_does_not_allow_reordering(bool reorderDivisions)
	{
		using var client = factory.CreateClient();
		var hierarchy = await CreateHierarchyAsync(client);
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader,
			ShikshaMitraSubmodulePermissions.InstituteSetup.AcademicSetup.View);
		var response = reorderDivisions
			? await client.PutAsJsonAsync(DivisionOrderRoute(hierarchy.ClassIds[0]), new { ids = hierarchy.DivisionIds.Reverse().ToArray() })
			: await client.PutAsJsonAsync(ClassOrderRoute(hierarchy.StageId), new { ids = hierarchy.ClassIds.Reverse().ToArray() });
		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
		var stage = await ReadStageAsync(client, hierarchy.StageId);
		stage.Standards.Select(x => x.Id).ShouldBe(hierarchy.ClassIds);
		stage.Standards.Single(x => x.Id == hierarchy.ClassIds[0]).Divisions.Select(x => x.Id).ShouldBe(hierarchy.DivisionIds);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Host_requests_cannot_reorder_tenant_classes_or_divisions(bool reorderDivisions)
	{
		using var client = factory.CreateClient();
		var hierarchy = await CreateHierarchyAsync(client);
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, "host");
		var response = reorderDivisions
			? await client.PutAsJsonAsync(DivisionOrderRoute(hierarchy.ClassIds[0]), new { ids = hierarchy.DivisionIds.Reverse().ToArray() })
			: await client.PutAsJsonAsync(ClassOrderRoute(hierarchy.StageId), new { ids = hierarchy.ClassIds.Reverse().ToArray() });
		response.StatusCode.ShouldBe(reorderDivisions ? HttpStatusCode.NotFound : HttpStatusCode.Forbidden,
			await response.Content.ReadAsStringAsync());
		client.DefaultRequestHeaders.Remove(TestAuthHandler.TestTenantIdHeader);
		var stage = await ReadStageAsync(client, hierarchy.StageId);
		stage.Standards.Select(x => x.Id).ShouldBe(hierarchy.ClassIds);
		stage.Standards.Single(x => x.Id == hierarchy.ClassIds[0]).Divisions.Select(x => x.Id).ShouldBe(hierarchy.DivisionIds);
	}

	private async Task<Hierarchy> CreateHierarchyAsync(HttpClient client)
	{
		var stage = await factory.SeedStageAsync();
		var classes = new List<Guid>();
		foreach (var name in new[] { "Class 1", "Class 2" })
		{
			var response = await client.PostAsJsonAsync("/api/app/standard", new { name, ageLowerLimit = 6, educationMediumStageId = stage });
			response.EnsureSuccessStatusCode();
			classes.Add((await response.Content.ReadFromJsonAsync<StandardDto>())!.Id);
		}
		var divisions = new List<Guid>();
		foreach (var name in new[] { "A", "B" })
		{
			var response = await client.PostAsJsonAsync("/api/app/division", new { name, strength = 30, standardId = classes[0] });
			response.EnsureSuccessStatusCode();
			divisions.Add((await response.Content.ReadFromJsonAsync<DivisionDto>())!.Id);
		}
		return new(stage, classes.ToArray(), divisions.ToArray());
	}

	private static async Task<EducationMediumStageDto> ReadStageAsync(HttpClient client, Guid stageId)
	{
		var boards = (await client.GetFromJsonAsync<PagedResultDto<EducationBoardDto>>("/api/app/education-board?MaxResultCount=1000"))!;
		return boards.Items.SelectMany(x => x.EducationMediums).SelectMany(x => x.EducationMediumStages).Single(x => x.Id == stageId);
	}

	private static Guid[] InvalidIds(Guid[] ids, string kind) => kind switch
	{
		"duplicate" => [ids[0], ids[0]],
		"partial" => [ids[0]],
		_ => [ids[0], Guid.NewGuid()]
	};

	private static string ClassOrderRoute(Guid stageId) => $"/api/education-medium-stages/{stageId}/standards/order";
	private static string DivisionOrderRoute(Guid standardId) => $"/api/standards/{standardId}/divisions/order";
	private sealed record Hierarchy(Guid StageId, Guid[] ClassIds, Guid[] DivisionIds);
}
