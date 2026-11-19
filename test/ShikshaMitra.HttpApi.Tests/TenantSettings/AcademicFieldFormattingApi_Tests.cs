using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.AcademicSetups.Formatting;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using ShikshaMitra.TenantSettings.AcademicSetups.Divisions;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.TenantSettings;

public class AcademicFieldFormattingApiFactory : AcademicSetupValidationFactory;

[Trait("Module", "TenantSettings"), Trait("Tier", "Critical")]
public class AcademicFieldFormattingApi_Tests(AcademicFieldFormattingApiFactory factory) : IClassFixture<AcademicFieldFormattingApiFactory>
{
	private const string Route = "/api/app/academic-field-formatting";

	[Fact]
	public async Task Missing_school_settings_default_to_keep_as_entered_and_are_tenant_isolated()
	{
		using var client = factory.CreateClient();
		await factory.SeedStageAsync();
		var otherTenant = Guid.NewGuid();
		await EnsureTenantAsync(otherTenant);
		(await client.PutAsJsonAsync(Route, Rules(AcademicTextCase.CamelCase))).EnsureSuccessStatusCode();
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, otherTenant.ToString());
		var defaults = (await client.GetFromJsonAsync<AcademicFieldFormattingDto>(Route))!;
		defaults.Board.ShouldBe(AcademicTextCase.Default);
		defaults.Medium.ShouldBe(AcademicTextCase.Default);
		defaults.Stage.ShouldBe(AcademicTextCase.Default);
		defaults.Standard.ShouldBe(AcademicTextCase.Default);
		defaults.Division.ShouldBe(AcademicTextCase.Default);
		client.DefaultRequestHeaders.Remove(TestAuthHandler.TestTenantIdHeader);
		(await client.GetFromJsonAsync<AcademicFieldFormattingDto>(Route))!.Stage.ShouldBe(AcademicTextCase.CamelCase);
	}

	[Theory]
	[InlineData(-1, 0)]
	[InlineData(5, 0)]
	[InlineData(2, 1)]
	[InlineData(4, 4)]
	public async Task Invalid_rules_are_rejected_without_partially_saving(int stage, int board)
	{
		using var client = factory.CreateClient();
		await factory.SeedStageAsync();
		(await client.PutAsJsonAsync(Route, Rules(AcademicTextCase.Default))).EnsureSuccessStatusCode();
		var response = await client.PutAsJsonAsync(Route, new { board, medium = 3, stage, standard = 3, division = 3 });
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
		var saved = (await client.GetFromJsonAsync<AcademicFieldFormattingDto>(Route))!;
		saved.Medium.ShouldBe(AcademicTextCase.Default);
		saved.Stage.ShouldBe(AcademicTextCase.Default);
		saved.Standard.ShouldBe(AcademicTextCase.Default);
	}

	[Fact]
	public async Task Host_and_missing_action_permissions_cannot_read_or_change_school_settings()
	{
		using var client = factory.CreateClient();
		await factory.SeedStageAsync();
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, ShikshaMitraSubmodulePermissions.InstituteSetup.AcademicSetup.View);
		(await client.GetAsync(Route)).EnsureSuccessStatusCode();
		(await client.PutAsJsonAsync(Route, Rules(AcademicTextCase.CamelCase))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		client.DefaultRequestHeaders.Remove(AlwaysGrantPermissionChecker.TestPermissionsHeader);
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, "none");
		(await client.GetAsync(Route)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		client.DefaultRequestHeaders.Remove(AlwaysGrantPermissionChecker.TestPermissionsHeader);
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, "host");
		(await client.GetAsync(Route)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await client.PutAsJsonAsync(Route, Rules(AcademicTextCase.Default))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task New_names_use_the_current_rule_once_while_saved_names_and_updates_keep_their_format()
	{
		using var client = factory.CreateClient();
		await factory.SeedStageAsync();
		(await client.PutAsJsonAsync(Route, Rules(AcademicTextCase.Default))).EnsureSuccessStatusCode();
		var originalStageId = await factory.SeedStageAsync();
		var stage = (await client.GetFromJsonAsync<EducationMediumStageDto>($"/api/app/education-medium-stage/{originalStageId}"))!;
		var originalResponse = await client.PostAsJsonAsync("/api/app/standard", Standard(stage.Id, "mIxEd class"));
		originalResponse.EnsureSuccessStatusCode();
		var original = (await originalResponse.Content.ReadFromJsonAsync<StandardDto>())!;
		(await client.PutAsJsonAsync(Route, Rules(AcademicTextCase.CamelCase))).EnsureSuccessStatusCode();
		var createdResponse = await client.PostAsJsonAsync("/api/app/standard", Standard(stage.Id, "secondary stage"));
		createdResponse.EnsureSuccessStatusCode();
		var created = (await createdResponse.Content.ReadFromJsonAsync<StandardDto>())!;
		created.Name.ShouldBe("secondaryStage");
		(await client.GetFromJsonAsync<StandardDto>($"/api/app/standard/{original.Id}"))!.Name.ShouldBe("mIxEd class");
		(await client.PutAsJsonAsync($"/api/app/standard/{original.Id}", Standard(stage.Id, original.Name, 7))).EnsureSuccessStatusCode();
		(await client.GetFromJsonAsync<StandardDto>($"/api/app/standard/{original.Id}"))!.Name.ShouldBe("mIxEd class");
		var duplicate = await client.PostAsJsonAsync("/api/app/standard", Standard(stage.Id, "secondary\tstage"));
		duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict, await duplicate.Content.ReadAsStringAsync());
		var divisionResponse = await client.PostAsJsonAsync("/api/app/division", new { name = "section one", strength = 40, standardId = created.Id });
		divisionResponse.EnsureSuccessStatusCode();
		(await divisionResponse.Content.ReadFromJsonAsync<DivisionDto>())!.Name.ShouldBe("sectionOne");
		var newStageResponse = await client.PostAsJsonAsync("/api/app/education-medium-stage", new { name = "senior secondary", educationMediumId = stage.EducationMediumId });
		newStageResponse.EnsureSuccessStatusCode();
		(await newStageResponse.Content.ReadFromJsonAsync<EducationMediumStageDto>())!.Name.ShouldBe("seniorSecondary");
		var medium = (await client.GetFromJsonAsync<EducationMediumDto>($"/api/app/education-medium/{stage.EducationMediumId}"))!;
		var mediumResponse = await client.PostAsJsonAsync("/api/app/education-medium", new { name = "regional language", educationBoardId = medium.EducationBoardId });
		mediumResponse.EnsureSuccessStatusCode();
		(await mediumResponse.Content.ReadFromJsonAsync<EducationMediumDto>())!.Name.ShouldBe("regionalLanguage");
		var spacedResponse = await client.PostAsJsonAsync("/api/app/education-medium-stage", new { name = "first" + new string(' ', 200) + "second", educationMediumId = stage.EducationMediumId });
		spacedResponse.EnsureSuccessStatusCode();
		(await spacedResponse.Content.ReadFromJsonAsync<EducationMediumStageDto>())!.Name.ShouldBe("firstSecond");
	}

	[Fact]
	public async Task Uppercase_rules_persist_for_each_mutable_field_and_keep_board_default()
	{
		using var client = factory.CreateClient();
		await factory.SeedStageAsync();
		var response = await client.PutAsJsonAsync(Route, Rules(AcademicTextCase.Uppercase));
		response.EnsureSuccessStatusCode();
		var saved = (await client.GetFromJsonAsync<AcademicFieldFormattingDto>(Route))!;
		saved.Board.ShouldBe(AcademicTextCase.Default);
		saved.Medium.ShouldBe(AcademicTextCase.Uppercase);
		saved.Stage.ShouldBe(AcademicTextCase.Uppercase);
		saved.Standard.ShouldBe(AcademicTextCase.Uppercase);
		saved.Division.ShouldBe(AcademicTextCase.Uppercase);
	}

	[Fact]
	public async Task Uppercase_formats_new_names_and_preserves_existing_names_during_other_edits()
	{
		using var client = factory.CreateClient();
		var stageId = await factory.SeedStageAsync();
		var stage = (await client.GetFromJsonAsync<EducationMediumStageDto>($"/api/app/education-medium-stage/{stageId}"))!;
		(await client.PutAsJsonAsync(Route, Rules(AcademicTextCase.Default))).EnsureSuccessStatusCode();
		var originalResponse = await client.PostAsJsonAsync("/api/app/standard", Standard(stage.Id, "mIxEd legacy"));
		originalResponse.EnsureSuccessStatusCode();
		var original = (await originalResponse.Content.ReadFromJsonAsync<StandardDto>())!;
		(await client.PutAsJsonAsync(Route, Rules(AcademicTextCase.Uppercase))).EnsureSuccessStatusCode();
		var standardResponse = await client.PostAsJsonAsync("/api/app/standard", Standard(stage.Id, "école class"));
		standardResponse.EnsureSuccessStatusCode();
		var standard = (await standardResponse.Content.ReadFromJsonAsync<StandardDto>())!;
		standard.Name.ShouldBe("ÉCOLE CLASS");
		var divisionResponse = await client.PostAsJsonAsync("/api/app/division", new { name = "section\t one", strength = 40, standardId = standard.Id });
		divisionResponse.EnsureSuccessStatusCode();
		(await divisionResponse.Content.ReadFromJsonAsync<DivisionDto>())!.Name.ShouldBe("SECTION\t ONE");
		var stageResponse = await client.PostAsJsonAsync("/api/app/education-medium-stage", new { name = "senior secondary", educationMediumId = stage.EducationMediumId });
		stageResponse.EnsureSuccessStatusCode();
		(await stageResponse.Content.ReadFromJsonAsync<EducationMediumStageDto>())!.Name.ShouldBe("SENIOR SECONDARY");
		var medium = (await client.GetFromJsonAsync<EducationMediumDto>($"/api/app/education-medium/{stage.EducationMediumId}"))!;
		var mediumResponse = await client.PostAsJsonAsync("/api/app/education-medium", new { name = "regional language", educationBoardId = medium.EducationBoardId });
		mediumResponse.EnsureSuccessStatusCode();
		(await mediumResponse.Content.ReadFromJsonAsync<EducationMediumDto>())!.Name.ShouldBe("REGIONAL LANGUAGE");
		(await client.GetFromJsonAsync<StandardDto>($"/api/app/standard/{original.Id}"))!.Name.ShouldBe("mIxEd legacy");
		(await client.PutAsJsonAsync($"/api/app/standard/{original.Id}", Standard(stage.Id, original.Name, 7))).EnsureSuccessStatusCode();
		var updated = (await client.GetFromJsonAsync<StandardDto>($"/api/app/standard/{original.Id}"))!;
		updated.Name.ShouldBe("mIxEd legacy");
		updated.AgeLowerLimit.ShouldBe(7);
	}

	private static AcademicFieldFormattingDto Rules(AcademicTextCase mode) => new() { Medium = mode, Stage = mode, Standard = mode, Division = mode };
	private static object Standard(Guid stageId, string name, int age = 6) => new { name, ageLowerLimit = age, educationMediumStageId = stageId };
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
