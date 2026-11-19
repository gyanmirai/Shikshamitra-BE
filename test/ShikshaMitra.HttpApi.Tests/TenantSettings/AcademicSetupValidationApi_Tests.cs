using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.Divisions;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Identity;
using Volo.Abp.TenantManagement;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.TenantSettings;

public class AcademicSetupValidationFactory : ShikshaMitraApiTestFactory
{
	protected override bool InitializeTestData => false;

	public async Task<Guid> SeedStageAsync(Guid? tenantId = null)
	{
		tenantId ??= Guid.Parse(TestAuthHandler.TestTenantId);
		using var scope = Services.CreateScope();
		var services = scope.ServiceProvider;
		using var unit = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var currentTenant = services.GetRequiredService<ICurrentTenant>();
		using (currentTenant.Change(null))
		{
			var tenants = services.GetRequiredService<IRepository<Tenant, Guid>>();
			if (await tenants.FindAsync(tenantId.Value) == null)
			{
				var school = await services.GetRequiredService<ITenantManager>().CreateAsync("Validation school " + tenantId);
				typeof(Tenant).GetProperty(nameof(Tenant.Id))!.SetValue(school, tenantId.Value);
				await tenants.InsertAsync(school, autoSave: true);
			}
		}
		using var tenant = currentTenant.Change(tenantId);
		var scopes = services.GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
		var userId = Guid.Parse(TestAuthHandler.TestUserId);
		var users = services.GetRequiredService<IIdentityUserRepository>();
		if (await users.FindAsync(userId) == null)
			await users.InsertAsync(new IdentityUser(userId, "regression-admin", "regression@example.test", tenantId), autoSave: true);
		if (!await scopes.AnyAsync(x => x.UserId == userId))
		{
			await scopes.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), tenantId.Value,
				userId: userId, description: "Academic Setup regression tenant-wide scope"), autoSave: true);
		}
		await services.GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(userId);
		var year = await services.GetRequiredService<AcademicYearManager>().CreateAsync(
			"Validation " + Guid.NewGuid().ToString("N")[..8], new DateTime(2026, 4, 1), new DateTime(2027, 3, 31));
		await services.GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(year, autoSave: true);
		var board = await services.GetRequiredService<EducationBoardManager>().CreateAsync(year.Id, EducationBoardType.CBSE);
		await services.GetRequiredService<IRepository<EducationBoard, Guid>>().InsertAsync(board, autoSave: true);
		var medium = await services.GetRequiredService<EducationMediumManager>().CreateAsync(board.Id, "English");
		await services.GetRequiredService<IRepository<EducationMedium, Guid>>().InsertAsync(medium, autoSave: true);
		var stage = await services.GetRequiredService<EducationMediumStageManager>().CreateAsync(medium.Id, "Primary");
		await services.GetRequiredService<IRepository<EducationMediumStage, Guid>>().InsertAsync(stage);
		await unit.CompleteAsync();
		return stage.Id;
	}

	public async Task<long> CountStandardsAsync(Guid stageId)
	{
		using var scope = Services.CreateScope();
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
		using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var query = await scope.ServiceProvider.GetRequiredService<IStandardRepository>().GetQueryableAsync();
		return await query.LongCountAsync(x => x.EducationMediumStageId == stageId);
	}
}

[Trait("Module", "TenantSettings"), Trait("Tier", "Critical")]
public class AcademicSetupValidationApi_Tests(AcademicSetupValidationFactory factory)
	: IClassFixture<AcademicSetupValidationFactory>
{
	[Fact]
	public async Task Education_board_options_should_contain_only_CBSE_and_GSEB()
	{
		using var client = factory.CreateClient();
		await factory.SeedStageAsync();
		var options = (await client.GetFromJsonAsync<EducationBoardOptionDto[]>("/api/app/education-board/options"))!;
		options.Length.ShouldBe(2);
		options[0].Value.ShouldBe(EducationBoardType.CBSE);
		options[0].Label.ShouldBe("CBSE");
		options[1].Value.ShouldBe(EducationBoardType.GSEB);
		options[1].Label.ShouldBe("GSEB");
	}

	[Theory]
	[InlineData(2)]
	[InlineData(3)]
	[InlineData(4)]
	[InlineData(5)]
	[InlineData(6)]
	public async Task Unsupported_education_boards_should_be_rejected_by_the_API(int type)
	{
		using var client = factory.CreateClient();
		await factory.SeedStageAsync();
		var response = await client.PostAsJsonAsync("/api/app/education-board", new {
			type, academicYearId = Guid.NewGuid(), customName = "Regional Board",
			startDate = new DateTime(2026, 4, 1), endDate = new DateTime(2027, 3, 31) });
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
		(await response.Content.ReadAsStringAsync()).ShouldContain(EducationBoardNames.InvalidType);
	}

	[Theory]
	[InlineData("Class 3")]
	[InlineData("class 3")]
	[InlineData(" Class 3 ")]
	public async Task Duplicate_standard_should_return_a_specific_conflict_without_inserting(string name)
	{
		using var client = factory.CreateClient();
		var stage = await factory.SeedStageAsync();
		var created = await client.PostAsJsonAsync("/api/app/standard", StandardInput(stage, "Class 3"));
		created.EnsureSuccessStatusCode();
		var response = await client.PostAsJsonAsync("/api/app/standard", StandardInput(stage, name));
		var body = await response.Content.ReadAsStringAsync();
		response.StatusCode.ShouldBe(HttpStatusCode.Conflict, body);
		body.ShouldContain("ShikshaMitra.TenantSettings:DuplicateStandardName");
		body.ShouldContain($"A standard named '{name.Trim()}' already exists in this academic stage.");
		body.ShouldNotContain("One or more business rules");
		(await factory.CountStandardsAsync(stage)).ShouldBe(1);
	}

	[Fact]
	public async Task Standard_names_should_be_compared_literally_instead_of_as_sql_patterns()
	{
		using var client = factory.CreateClient();
		var stage = await factory.SeedStageAsync();
		(await client.PostAsJsonAsync("/api/app/standard", StandardInput(stage, "Class 3"))).EnsureSuccessStatusCode();
		(await client.PostAsJsonAsync("/api/app/standard", StandardInput(stage, "Class %"))).EnsureSuccessStatusCode();
		(await factory.CountStandardsAsync(stage)).ShouldBe(2);
	}

	[Fact]
	public async Task Renaming_to_a_duplicate_should_fail_but_case_only_self_edits_and_other_stages_should_succeed()
	{
		using var client = factory.CreateClient();
		var stage = await factory.SeedStageAsync();
		var first = await client.PostAsJsonAsync("/api/app/standard", StandardInput(stage, "Class 3"));
		var original = (await first.Content.ReadFromJsonAsync<StandardDto>())!;
		var second = await client.PostAsJsonAsync("/api/app/standard", StandardInput(stage, "Class 4"));
		var target = (await second.Content.ReadFromJsonAsync<StandardDto>())!;
		var duplicate = await client.PutAsJsonAsync($"/api/app/standard/{target.Id}", StandardInput(stage, "Class 3"));
		duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict, await duplicate.Content.ReadAsStringAsync());
		var self = await client.PutAsJsonAsync($"/api/app/standard/{original.Id}", StandardInput(stage, "class 3"));
		self.EnsureSuccessStatusCode();
		var otherStage = await factory.SeedStageAsync();
		(await client.PostAsJsonAsync("/api/app/standard", StandardInput(otherStage, "Class 3"))).EnsureSuccessStatusCode();
		(await client.GetFromJsonAsync<StandardDto>($"/api/app/standard/{target.Id}"))!.Name.ShouldBe("Class 4");
	}

	[Fact]
	public async Task Editing_without_a_room_field_should_preserve_an_existing_allocation()
	{
		using var client = factory.CreateClient();
		var stage = await factory.SeedStageAsync();
		var standardResponse = await client.PostAsJsonAsync("/api/app/standard", StandardInput(stage, "Class 1"));
		var standard = (await standardResponse.Content.ReadFromJsonAsync<StandardDto>())!;
		var created = await client.PostAsJsonAsync("/api/app/division", new {
			name = "A", strength = 50, standardId = standard.Id, roomNumber = "B101" });
		created.EnsureSuccessStatusCode();
		var division = (await created.Content.ReadFromJsonAsync<DivisionDto>())!;
		var update = await client.PutAsJsonAsync($"/api/app/division/{division.Id}", new {
			name = "A", strength = 45, standardId = standard.Id });
		update.EnsureSuccessStatusCode();
		var restored = (await client.GetFromJsonAsync<DivisionDto>($"/api/app/division/{division.Id}"))!;
		restored.Strength.ShouldBe(45);
		restored.RoomNumber.ShouldBe("B101");
	}

	private static object StandardInput(Guid stage, string name) => new {
		name, ageLowerLimit = 6, educationMediumStageId = stage };
}
