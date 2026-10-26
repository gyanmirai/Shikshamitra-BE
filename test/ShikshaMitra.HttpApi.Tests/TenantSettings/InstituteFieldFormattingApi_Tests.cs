using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.TenantSettings.AcademicSetups.Formatting;
using ShikshaMitra.TenantSettings.Formatting;
using ShikshaMitra.TenantSettings.RoomsManagement.Buildings;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using ShikshaMitra.TenantSettings.RoomsManagement.RoomTypes;
using ShikshaMitra.TenantSettings.SchoolDocuments;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.TenantSettings;

public class InstituteFieldFormattingApiFactory : AcademicSetupValidationFactory;

[Trait("Module", "TenantSettings"), Trait("Tier", "Critical")]
public partial class InstituteFieldFormattingApi_Tests(InstituteFieldFormattingApiFactory factory) : IClassFixture<InstituteFieldFormattingApiFactory>
{
	private const string Route = "/api/app/institute-field-formatting/";

	[Theory]
	[InlineData("buildings", "buildingName", 3)]
	[InlineData("roomTypes", "shortName", 2)]
	[InlineData("rooms", "roomName", 2)]
	[InlineData("shifts", "shiftName", 1)]
	[InlineData("schoolIdentity", "designation", 2)]
	[InlineData("schoolDocuments", "notes", 2)]
	public async Task Rules_persist_with_exact_scope_fields_and_tenant_isolation(string scope, string field, int count)
	{
		using var client = factory.CreateClient();
		await factory.SeedStageAsync();
		var rules = (await client.GetFromJsonAsync<InstituteFieldFormattingDto>(Route + scope))!;
		rules.Fields.Count.ShouldBe(count);
		rules.Fields[field] = AcademicTextCase.Uppercase;
		(await client.PutAsJsonAsync(Route + scope, rules)).EnsureSuccessStatusCode();
		(await client.GetFromJsonAsync<InstituteFieldFormattingDto>(Route + scope))!.Fields[field].ShouldBe(AcademicTextCase.Uppercase);
		var other = Guid.NewGuid();
		await EnsureTenantAsync(other);
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, other.ToString());
		(await client.GetFromJsonAsync<InstituteFieldFormattingDto>(Route + scope))!.Fields.Values.ShouldAllBe(mode => mode == AcademicTextCase.Default);
	}

	[Fact]
	public async Task Invalid_fields_modes_and_scopes_do_not_overwrite_saved_settings()
	{
		using var client = factory.CreateClient();
		await factory.SeedStageAsync();
		await SetAsync(client, "rooms", AcademicTextCase.Uppercase);
		foreach (var fields in new Dictionary<string, int>[] {
			new(), new() { ["roomName"] = 5, ["featureName"] = 0 }, new() { ["roomName"] = 2, ["shiftName"] = 3 } })
			(await client.PutAsJsonAsync(Route + "rooms", new { fields })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await client.GetFromJsonAsync<InstituteFieldFormattingDto>(Route + "rooms"))!.Fields["roomName"].ShouldBe(AcademicTextCase.Uppercase);
		(await client.GetAsync(Route + "unknown")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Scope_permissions_are_independent_of_academic_access_and_host_access_is_denied()
	{
		using var client = factory.CreateClient();
		await factory.SeedStageAsync();
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, "InstituteSetup.ShiftManagement.View");
		(await client.GetAsync(Route + "shifts")).EnsureSuccessStatusCode();
		(await client.PostAsJsonAsync(Route + "shifts/preview", new { value = "école stage", textCase = 1 })).EnsureSuccessStatusCode();
		(await client.GetAsync(Route + "rooms")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await client.PutAsJsonAsync(Route + "shifts", new { fields = new { shiftName = 4 } })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		client.DefaultRequestHeaders.Remove(AlwaysGrantPermissionChecker.TestPermissionsHeader);
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, "host");
		(await client.GetAsync(Route + "shifts")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await client.PutAsJsonAsync(Route + "shifts", new { fields = new { shiftName = 4 } })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task Preview_accepts_full_notes_and_rejects_values_beyond_the_contract()
	{
		using var client = factory.CreateClient();
		await factory.SeedStageAsync();
		var response = await client.PostAsJsonAsync(Route + "schoolDocuments/preview", new { value = new string('é', 2000), textCase = 4 });
		response.EnsureSuccessStatusCode();
		(await response.Content.ReadFromJsonAsync<AcademicNamePreviewDto>())!.Value.ShouldBe(new string('É', 2000));
		(await client.PostAsJsonAsync(Route + "schoolDocuments/preview", new { value = new string('a', 2001), textCase = 4 })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Creates_apply_saved_rules_and_new_floors_format_without_rewriting_existing_records()
	{
		using var client = factory.CreateClient();
		await factory.SeedStageAsync();
		await SetAsync(client, "buildings", AcademicTextCase.CamelCase);
		await SetAsync(client, "roomTypes", AcademicTextCase.Uppercase);
		await SetAsync(client, "rooms", AcademicTextCase.PascalCase);
		await SetAsync(client, "shifts", AcademicTextCase.Capitalize);
		var featureResponse = await client.PostAsJsonAsync("/api/institute-setup/rooms/features", new { name = "science equipment" });
		featureResponse.EnsureSuccessStatusCode();
		var feature = (await featureResponse.Content.ReadFromJsonAsync<RoomFeatureDto>())!;
		feature.Name.ShouldBe("ScienceEquipment");
		var duplicateFeature = await client.PostAsJsonAsync("/api/institute-setup/rooms/features", new { name = "science equipment" });
		duplicateFeature.EnsureSuccessStatusCode();
		(await duplicateFeature.Content.ReadFromJsonAsync<RoomFeatureDto>())!.Id.ShouldBe(feature.Id);
		var response = await client.PostAsJsonAsync("/api/institute-setup/buildings", new {
			buildingName = "north wing", totalFloors = 1, description = "main teaching wing",
			floors = new[] { new { floorName = "ground floor", floorNo = 0, totalRoom = 10 } } });
		response.EnsureSuccessStatusCode();
		var building = (await response.Content.ReadFromJsonAsync<BuildingDto>())!;
		building.BuildingName.ShouldBe("northWing");
		building.Description.ShouldBe("mainTeachingWing");
		building.Floors.Single().FloorName.ShouldBe("groundFloor");
		var typeResponse = await client.PostAsJsonAsync("/api/institute-setup/rooms-management/room-types", new { roomTypeName = "science lab", shortName = "lab" });
		typeResponse.EnsureSuccessStatusCode();
		var roomType = (await typeResponse.Content.ReadFromJsonAsync<RoomTypeDto>())!;
		roomType.RoomTypeName.ShouldBe("SCIENCE LAB");
		roomType.ShortName.ShouldBe("LAB");
		var roomResponse = await client.PostAsJsonAsync("/api/institute-setup/rooms", new { roomName = "science one", capacity = 20, buildingId = building.Id,
			floorId = building.Floors.Single().Id, roomTypeId = roomType.Id, totalBenches = 0, benchCapacity = 0, benchesPerRow = 0, benchesPerColumn = 0 });
		roomResponse.EnsureSuccessStatusCode();
		(await roomResponse.Content.ReadFromJsonAsync<RoomDto>())!.RoomName.ShouldBe("ScienceOne");
		var shiftResponse = await client.PostAsJsonAsync("/api/app/shift-management", new { shiftName = "morning shift", startTime = "08:00:00", endTime = "12:00:00" });
		shiftResponse.EnsureSuccessStatusCode();
		var shift = (await shiftResponse.Content.ReadFromJsonAsync<ShiftManagementDto>())!;
		shift.ShiftName.ShouldBe("Morning Shift");
		await SetAsync(client, "shifts", AcademicTextCase.Uppercase);
		(await client.PutAsJsonAsync("/api/app/shift-management/" + shift.Id, new { shiftName = shift.ShiftName, startTime = "09:00:00", endTime = "13:00:00" })).EnsureSuccessStatusCode();
		(await client.GetFromJsonAsync<ShiftManagementDto>("/api/app/shift-management/" + shift.Id))!.ShiftName.ShouldBe("Morning Shift");
		await SetAsync(client, "buildings", AcademicTextCase.Uppercase);
		var update = await client.PutAsJsonAsync("/api/institute-setup/buildings/" + building.Id, new {
			buildingName = building.BuildingName, description = building.Description, totalFloors = 2,
			floorsToUpdate = building.Floors.Select(floor => new { floor.Id, floor.FloorName, floor.FloorNo, floor.TotalRoom }),
			floorsToAdd = new[] { new { floorName = "first floor", floorNo = 1, totalRoom = 10 } }, floorIdsToDelete = Array.Empty<Guid>() });
		update.EnsureSuccessStatusCode();
		var updated = (await update.Content.ReadFromJsonAsync<BuildingDto>())!;
		updated.BuildingName.ShouldBe("northWing");
		updated.Floors.Single(floor => floor.FloorNo == 0).FloorName.ShouldBe("groundFloor");
		updated.Floors.Single(floor => floor.FloorNo == 1).FloorName.ShouldBe("FIRST FLOOR");
	}

	[Fact]
	public async Task New_document_details_follow_rules_while_attachment_edits_preserve_existing_text()
	{
		using var client = factory.CreateClient();
		await factory.SeedStageAsync();
		await SetAsync(client, "schoolDocuments", AcademicTextCase.Capitalize);
		using var form = DocumentForm("board approval", "government approval notes");
		var response = await client.PostAsync("/api/institute-setup/school-documents", form);
		response.EnsureSuccessStatusCode();
		var document = (await response.Content.ReadFromJsonAsync<SchoolDocumentDto>())!;
		document.Name.ShouldBe("Board Approval");
		document.Notes.ShouldBe("Government Approval Notes");
		await SetAsync(client, "schoolDocuments", AcademicTextCase.Uppercase);
		using var update = DocumentForm(document.Name, document.Notes!, document);
		var updated = await client.PutAsync("/api/institute-setup/school-documents/" + document.Id, update);
		updated.EnsureSuccessStatusCode();
		var saved = (await updated.Content.ReadFromJsonAsync<SchoolDocumentDto>())!;
		saved.Name.ShouldBe("Board Approval");
		saved.Notes.ShouldBe("Government Approval Notes");
	}

	private static MultipartFormDataContent DocumentForm(string name, string notes, SchoolDocumentDto? original = null)
	{
		var form = new MultipartFormDataContent();
		form.Add(new StringContent(name), "name");
		form.Add(new StringContent(notes), "notes");
		form.Add(new StringContent("0"), "category");
		if (original != null)
		{
			form.Add(new StringContent(original.ConcurrencyStamp), "concurrencyStamp");
			foreach (var file in original.Files) form.Add(new StringContent(file.Id.ToString()), "retainedFileIds");
		}
		else
		{
			var file = new ByteArrayContent(Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aRZkAAAAASUVORK5CYII="));
			file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
			form.Add(file, "files", "approval.png");
		}
		return form;
	}

	private static async Task SetAsync(HttpClient client, string scope, AcademicTextCase mode)
	{
		var rules = (await client.GetFromJsonAsync<InstituteFieldFormattingDto>(Route + scope))!;
		foreach (var key in rules.Fields.Keys.ToArray()) rules.Fields[key] = mode;
		(await client.PutAsJsonAsync(Route + scope, rules)).EnsureSuccessStatusCode();
	}

	private async Task EnsureTenantAsync(Guid tenantId)
	{
		using var scope = factory.Services.CreateScope();
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(null);
		using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var entity = await scope.ServiceProvider.GetRequiredService<ITenantManager>().CreateAsync("Formatting test " + tenantId);
		typeof(Tenant).GetProperty(nameof(Tenant.Id))!.SetValue(entity, tenantId);
		await scope.ServiceProvider.GetRequiredService<IRepository<Tenant, Guid>>().InsertAsync(entity, autoSave: true);
		await unit.CompleteAsync();
	}
}
