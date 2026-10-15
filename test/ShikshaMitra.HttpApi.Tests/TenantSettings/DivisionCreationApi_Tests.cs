using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.TenantSettings.AcademicSetups.Divisions;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using Shouldly;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.TenantSettings;

[Trait("Module", "TenantSettings"), Trait("Tier", "Critical")]
public class DivisionCreationApi_Tests(AcademicSetupValidationFactory factory)
	: IClassFixture<AcademicSetupValidationFactory>
{
	[Theory]
	[InlineData("A")]
	[InlineData("a")]
	[InlineData(" A ")]
	public async Task Duplicate_divisions_return_a_specific_conflict_and_do_not_insert(string name)
	{
		using var client = factory.CreateClient();
		var standard = await CreateStandardAsync(client);
		(await client.PostAsJsonAsync("/api/app/division", Input(standard, "A"))).EnsureSuccessStatusCode();
		var response = await client.PostAsJsonAsync("/api/app/division", Input(standard, name));
		var body = await response.Content.ReadAsStringAsync();
		response.StatusCode.ShouldBe(HttpStatusCode.Conflict, body);
		body.ShouldContain("ShikshaMitra.TenantSettings:DuplicateDivisionName");
		body.ShouldContain($"A division named '{name.Trim()}' already exists in this standard.");
		var saved = (await client.GetFromJsonAsync<StandardDto>($"/api/app/standard/{standard}"))!;
		saved.Divisions.Count.ShouldBe(1);
	}

	[Fact]
	public async Task Omitted_or_null_rooms_allow_creation_and_names_are_trimmed()
	{
		using var client = factory.CreateClient();
		var standard = await CreateStandardAsync(client);
		foreach (var name in new[] { "B", "C" })
		{
			var response = name == "B"
				? await client.PostAsJsonAsync("/api/app/division", Input(standard, " B "))
				: await client.PostAsJsonAsync("/api/app/division", new { name, strength = 50, standardId = standard, roomNumber = (string?)null });
			response.EnsureSuccessStatusCode();
			var division = (await response.Content.ReadFromJsonAsync<DivisionDto>())!;
			var saved = (await client.GetFromJsonAsync<DivisionDto>($"/api/app/division/{division.Id}"))!;
			saved.Name.ShouldBe(name);
			saved.Strength.ShouldBe(50);
			saved.RoomNumber.ShouldBe(string.Empty);
		}
	}

	[Fact]
	public async Task Rename_conflicts_preserve_data_but_case_only_self_edits_succeed()
	{
		using var client = factory.CreateClient();
		var standard = await CreateStandardAsync(client);
		var first = await client.PostAsJsonAsync("/api/app/division", Input(standard, "A"));
		var original = (await first.Content.ReadFromJsonAsync<DivisionDto>())!;
		var second = await client.PostAsJsonAsync("/api/app/division", Input(standard, "B"));
		var target = (await second.Content.ReadFromJsonAsync<DivisionDto>())!;
		var conflict = await client.PutAsJsonAsync($"/api/app/division/{target.Id}", Input(standard, " a "));
		conflict.StatusCode.ShouldBe(HttpStatusCode.Conflict, await conflict.Content.ReadAsStringAsync());
		(await client.GetFromJsonAsync<DivisionDto>($"/api/app/division/{target.Id}"))!.Name.ShouldBe("B");
		var self = await client.PutAsJsonAsync($"/api/app/division/{original.Id}", Input(standard, "a"));
		self.EnsureSuccessStatusCode();
		(await client.GetFromJsonAsync<DivisionDto>($"/api/app/division/{original.Id}"))!.Name.ShouldBe("a");
	}

	[Fact]
	public async Task Names_are_literal_and_scoped_to_the_standard_and_current_tenant()
	{
		using var client = factory.CreateClient();
		var standard = await CreateStandardAsync(client);
		foreach (var name in new[] { "A", "%", "_" })
			(await client.PostAsJsonAsync("/api/app/division", Input(standard, name))).EnsureSuccessStatusCode();
		var otherStandard = await CreateStandardAsync(client);
		(await client.PostAsJsonAsync("/api/app/division", Input(otherStandard, "A"))).EnsureSuccessStatusCode();
		using var scope = factory.Services.CreateScope();
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid());
		using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		(await scope.ServiceProvider.GetRequiredService<IDivisionRepository>()
			.FindByNameAndStandardIdAsync("A", standard)).ShouldBeNull();
	}

	private async Task<Guid> CreateStandardAsync(System.Net.Http.HttpClient client)
	{
		var stage = await factory.SeedStageAsync();
		var response = await client.PostAsJsonAsync("/api/app/standard", new { name = "KG", ageLowerLimit = 5, educationMediumStageId = stage });
		response.EnsureSuccessStatusCode();
		return (await response.Content.ReadFromJsonAsync<StandardDto>())!.Id;
	}

	private static object Input(Guid standard, string name) => new { name, strength = 50, standardId = standard };
}
