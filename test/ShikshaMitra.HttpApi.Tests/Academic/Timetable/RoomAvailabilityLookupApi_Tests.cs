using System;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.TenantSettings.RoomsManagement.Buildings;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Academic.Timetable;

[Collection(ApiTestCollection.Name)]
public class RoomAvailabilityLookupApi_Tests(ShikshaMitraApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	private const string Lookup = "/api/academics/timetable/room-availability/buildings/lookup";
	private const string View = ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.View;

	[Fact]
	public async Task Timetable_view_should_allow_lookup_without_granting_building_management()
	{
		var own = await SeedBuildingAsync(Guid.Parse(TestAuthHandler.TestTenantId), "Own lookup building");
		var foreign = await SeedBuildingAsync(Guid.NewGuid(), "Foreign lookup building");
		using var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, View);
		using var response = await client.GetAsync(Lookup);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
		var buildings = body.RootElement.EnumerateArray().ToArray();
		buildings.ShouldNotContain(row => row.GetProperty("id").GetGuid() == foreign);
		var building = buildings.Single(row => row.GetProperty("id").GetGuid() == own);
		building.EnumerateObject().Select(property => property.Name).OrderBy(name => name)
			.ShouldBe(new[] { "buildingName", "floors", "id" });
		building.GetProperty("floors").EnumerateArray().Single().GetProperty("floorName").GetString().ShouldBe("First floor");
		(await client.GetAsync("/api/institute-setup/buildings")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Theory]
	[InlineData("__none__")]
	[InlineData(ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.Add)]
	[InlineData(ShikshaMitraSubmodulePermissions.InstituteSetup.BuildingManagement.View)]
	public async Task Lookup_should_require_timetable_view(string permission)
	{
		using var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, permission);
		(await client.GetAsync(Lookup)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task Lookup_should_require_authentication()
	{
		using var anonymous = CreateAnonymousClient();
		(await anonymous.GetAsync(Lookup)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
	}

	private async Task<Guid> SeedBuildingAsync(Guid tenantId, string name)
	{
		using var scope = Factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(tenantId);
		using var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var building = new Building { TenantId = tenantId, BuildingName = name, TotalFloors = 1, Description = "Management-only details" };
		typeof(Building).GetProperty(nameof(Building.Id))!.SetValue(building, Guid.NewGuid());
		await services.GetRequiredService<IRepository<Building, Guid>>().InsertAsync(building, autoSave: true);
		var floor = new Floor { BuildingId = building.Id, FloorName = "First floor", FloorNo = 1, TotalRoom = 20 };
		typeof(Floor).GetProperty(nameof(Floor.Id))!.SetValue(floor, Guid.NewGuid());
		await services.GetRequiredService<IRepository<Floor, Guid>>().InsertAsync(floor, autoSave: true);
		await uow.CompleteAsync();
		return building.Id;
	}
}
