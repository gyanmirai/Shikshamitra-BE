using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.TenantSettings;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.TenantSettings)]
public partial class AcademicOrganizationOptionsApi_Tests : ShikshaMitraApiTestBase
{
	private const string OptionsUrl = "/api/app/academic-organization-options";

	public AcademicOrganizationOptionsApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Options_Should_Return_All_Current_Year_Stages_For_Tenant_Wide_User()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var seeded = await SeedAcademicOrganizationsAsync();

		var response = await Client.GetAsync($"{OptionsUrl}?academicYearId={seeded.AcademicYearId:D}");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var payload = await response.Content.ReadFromJsonAsync<AcademicOrganizationOptionsResponse>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize academic organization options.");

		payload.Items.Select(x => x.Id).ShouldContain(seeded.GsebGujaratiPrimaryStageId);
		payload.Items.Select(x => x.Id).ShouldContain(seeded.CbseEnglishSecondaryStageId);
		payload.Items.Select(x => x.Id).ShouldNotContain(seeded.OtherYearStageId);

		var gseb = payload.Items.Single(x => x.Id == seeded.GsebGujaratiPrimaryStageId);
		gseb.Label.ShouldBe("GSEB - Gujarati - Primary");
		gseb.BoardName.ShouldBe("GSEB");
		gseb.MediumName.ShouldBe("Gujarati");
		gseb.StageName.ShouldBe("Primary");
		gseb.AcademicYearId.ShouldBe(seeded.AcademicYearId);

		var returned = payload.Items
			.Where(x => x.Id == seeded.CbseEnglishSecondaryStageId || x.Id == seeded.GsebGujaratiPrimaryStageId)
			.ToList();
		returned.Select(x => x.Label).ShouldBe(["CBSE - English - Secondary", "GSEB - Gujarati - Primary"]);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Options_Should_Apply_User_Stage_Scope()
	{
		var seeded = await SeedAcademicOrganizationsAsync();
		var scopedUserId = Guid.NewGuid();
		await EnsureUserHasStageScopeAsync(scopedUserId, seeded.GsebGujaratiPrimaryStageId);

		var scopedClient = CreateClientAs(scopedUserId);
		var response = await scopedClient.GetAsync($"{OptionsUrl}?academicYearId={seeded.AcademicYearId:D}");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var payload = await response.Content.ReadFromJsonAsync<AcademicOrganizationOptionsResponse>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize academic organization options.");

		payload.Items.Select(x => x.Id).ShouldBe([seeded.GsebGujaratiPrimaryStageId]);
		payload.Items.Single().Label.ShouldBe("GSEB - Gujarati - Primary");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Standards_Should_Honor_Stage_And_Board_Wildcard_Scope()
	{
		var seeded = await SeedAcademicOrganizationsAsync();

		var stageScopedUserId = Guid.NewGuid();
		await EnsureUserHasStageScopeAsync(
			stageScopedUserId,
			seeded.GsebGujaratiPrimaryStageId);
		var stageScopedClient = CreateClientAs(stageScopedUserId);

		var stageResponse = await stageScopedClient.GetAsync(
			$"/api/education-medium/{seeded.GsebGujaratiMediumId:D}/standards");
		stageResponse.StatusCode.ShouldBe(
			HttpStatusCode.OK,
			await stageResponse.Content.ReadAsStringAsync());
		var stageStandards = await stageResponse.Content.ReadFromJsonAsync<StandardResponse[]>(
			JsonOptions) ?? [];
		stageStandards.Select(x => x.Id).ShouldBe([seeded.GsebPrimaryStandardId]);

		var restrictedStageResponse = await stageScopedClient.GetAsync(
			$"/api/education-medium/{seeded.CbseEnglishMediumId:D}/standards");
		restrictedStageResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var restrictedStageStandards =
			await restrictedStageResponse.Content.ReadFromJsonAsync<StandardResponse[]>(JsonOptions)
			?? [];
		restrictedStageStandards.ShouldBeEmpty();

		var boardScopedUserId = Guid.NewGuid();
		await EnsureUserHasBoardScopeAsync(
			boardScopedUserId,
			seeded.AcademicYearId,
			seeded.CbseBoardId);
		var boardScopedClient = CreateClientAs(boardScopedUserId);

		var boardResponse = await boardScopedClient.GetAsync(
			$"/api/education-medium/{seeded.CbseEnglishMediumId:D}/standards");
		boardResponse.StatusCode.ShouldBe(
			HttpStatusCode.OK,
			await boardResponse.Content.ReadAsStringAsync());
		var boardStandards = await boardResponse.Content.ReadFromJsonAsync<StandardResponse[]>(
			JsonOptions) ?? [];
		boardStandards.Select(x => x.Id).ShouldBe([seeded.CbseSecondaryStandardId]);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Options_Should_Not_Return_Restricted_Boards_For_Board_Scoped_User()
	{
		var seeded = await SeedAcademicOrganizationsAsync();
		var scopedUserId = Guid.NewGuid();
		await EnsureUserHasBoardScopeAsync(scopedUserId, seeded.AcademicYearId, seeded.CbseBoardId);

		var scopedClient = CreateClientAs(scopedUserId);
		var response = await scopedClient.GetAsync($"{OptionsUrl}?academicYearId={seeded.AcademicYearId:D}");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var payload = await response.Content.ReadFromJsonAsync<AcademicOrganizationOptionsResponse>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize academic organization options.");

		payload.Items.Select(x => x.Id).ShouldBe([seeded.CbseEnglishSecondaryStageId]);
		payload.Items.Select(x => x.BoardName).ShouldBe(["CBSE"]);
		payload.Items.Select(x => x.Label).ShouldNotContain("GSEB - Gujarati - Primary");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Options_Should_Require_AcademicYearId()
	{
		var response = await Client.GetAsync(OptionsUrl);

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	private async Task<SeededAcademicOrganizations> SeedAcademicOrganizationsAsync(Guid? tenantOverride = null)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var tenantId = tenantOverride ?? Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var academicYearRepository = scope.ServiceProvider.GetRequiredService<IAcademicYearRepository>();
			var boardRepository = scope.ServiceProvider.GetRequiredService<IEducationBoardRepository>();
			var mediumRepository = scope.ServiceProvider.GetRequiredService<IEducationMediumRepository>();
			var stageRepository = scope.ServiceProvider.GetRequiredService<IEducationMediumStageRepository>();
			var standardRepository = scope.ServiceProvider.GetRequiredService<IStandardRepository>();
			var academicYearManager = scope.ServiceProvider.GetRequiredService<AcademicYearManager>();
			var boardManager = scope.ServiceProvider.GetRequiredService<EducationBoardManager>();
			var mediumManager = scope.ServiceProvider.GetRequiredService<EducationMediumManager>();
			var stageManager = scope.ServiceProvider.GetRequiredService<EducationMediumStageManager>();
			var standardManager = scope.ServiceProvider.GetRequiredService<StandardManager>();

			var suffix = Guid.NewGuid().ToString("N")[..8];
			var academicYear = await academicYearManager.CreateAsync(
				$"OrgOpt-{suffix}",
				new DateTime(2031, 4, 1),
				new DateTime(2032, 3, 31));
			await academicYearRepository.InsertAsync(academicYear, autoSave: false);

			var otherYear = await academicYearManager.CreateAsync(
				$"OrgOptOther-{suffix}",
				new DateTime(2032, 4, 1),
				new DateTime(2033, 3, 31));
			await academicYearRepository.InsertAsync(otherYear, autoSave: false);

			var gseb = await boardManager.CreateAsync(academicYear.Id, EducationBoardType.GSEB);
			await boardRepository.InsertAsync(gseb, autoSave: false);
			var cbse = await boardManager.CreateAsync(academicYear.Id, EducationBoardType.CBSE);
			await boardRepository.InsertAsync(cbse, autoSave: false);
			var otherYearBoard = await boardManager.CreateAsync(otherYear.Id, EducationBoardType.GSEB);
			await boardRepository.InsertAsync(otherYearBoard, autoSave: false);

			var gujarati = await mediumManager.CreateAsync(gseb.Id, "Gujarati");
			await mediumRepository.InsertAsync(gujarati, autoSave: false);
			var english = await mediumManager.CreateAsync(cbse.Id, "English");
			await mediumRepository.InsertAsync(english, autoSave: false);
			var otherYearMedium = await mediumManager.CreateAsync(otherYearBoard.Id, "Gujarati");
			await mediumRepository.InsertAsync(otherYearMedium, autoSave: false);

			var primary = await stageManager.CreateAsync(gujarati.Id, "Primary");
			await stageRepository.InsertAsync(primary, autoSave: false);
			var secondary = await stageManager.CreateAsync(english.Id, "Secondary");
			await stageRepository.InsertAsync(secondary, autoSave: false);
			var otherYearStage = await stageManager.CreateAsync(otherYearMedium.Id, "Primary");
			await stageRepository.InsertAsync(otherYearStage, autoSave: false);

			var primaryStandard = await standardManager.CreateAsync(
				primary.Id,
				"Class 4",
				9,
				shiftId: null);
			await standardRepository.InsertAsync(primaryStandard, autoSave: false);
			var secondaryStandard = await standardManager.CreateAsync(
				secondary.Id,
				"Class 8",
				13,
				shiftId: null);
			await standardRepository.InsertAsync(secondaryStandard, autoSave: false);

			await uow.CompleteAsync();

			return new SeededAcademicOrganizations(
				academicYear.Id,
				gseb.Id,
				cbse.Id,
				gujarati.Id,
				english.Id,
				primary.Id,
				secondary.Id,
				primaryStandard.Id,
				secondaryStandard.Id,
				otherYearStage.Id);
		}
	}

	private async Task EnsureUserHasStageScopeAsync(Guid userId, Guid stageId)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var scopeRepository = scope.ServiceProvider.GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
		var userScopeService = scope.ServiceProvider.GetRequiredService<IUserScopeService>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			await EnsureScopedIdentityAsync(scope.ServiceProvider, userId, tenantId);
			await scopeRepository.InsertAsync(
				new UserScopeAssignment(
					Guid.NewGuid(),
					tenantId,
					userId: userId,
					educationMediumStageId: stageId,
					description: "API regression test academic organization stage scope"),
				autoSave: false);
			await userScopeService.InvalidateUserScopeCacheAsync(userId);
			await uow.CompleteAsync();
		}
	}

	private async Task EnsureUserHasBoardScopeAsync(Guid userId, Guid academicYearId, Guid boardId)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var scopeRepository = scope.ServiceProvider.GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
		var userScopeService = scope.ServiceProvider.GetRequiredService<IUserScopeService>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			await EnsureScopedIdentityAsync(scope.ServiceProvider, userId, tenantId);
			await scopeRepository.InsertAsync(
				new UserScopeAssignment(
					Guid.NewGuid(),
					tenantId,
					userId: userId,
					academicYearId: academicYearId,
					educationBoardId: boardId,
					description: "API regression test academic organization board scope"),
				autoSave: false);
			await userScopeService.InvalidateUserScopeCacheAsync(userId);
			await uow.CompleteAsync();
		}
	}

	private static async Task EnsureScopedIdentityAsync(IServiceProvider services, Guid userId, Guid tenantId)
	{
		var users = services.GetRequiredService<IIdentityUserRepository>();
		if (await users.FindAsync(userId) == null)
			await users.InsertAsync(new IdentityUser(userId, $"orgopt-{userId:N}", $"orgopt-{userId:N}@example.test", tenantId));
	}

	private sealed record SeededAcademicOrganizations(
		Guid AcademicYearId,
		Guid GsebBoardId,
		Guid CbseBoardId,
		Guid GsebGujaratiMediumId,
		Guid CbseEnglishMediumId,
		Guid GsebGujaratiPrimaryStageId,
		Guid CbseEnglishSecondaryStageId,
		Guid GsebPrimaryStandardId,
		Guid CbseSecondaryStandardId,
		Guid OtherYearStageId);

	private sealed class AcademicOrganizationOptionsResponse
	{
		public AcademicOrganizationOptionResponse[] Items { get; set; } = [];
	}

	private sealed class AcademicOrganizationOptionResponse
	{
		public Guid Id { get; set; }
		public string Label { get; set; } = string.Empty;
		public string StageName { get; set; } = string.Empty;
		public Guid MediumId { get; set; }
		public string MediumName { get; set; } = string.Empty;
		public Guid BoardId { get; set; }
		public string BoardName { get; set; } = string.Empty;
		public Guid AcademicYearId { get; set; }
	}

	private sealed class StandardResponse
	{
		public Guid Id { get; set; }
		public Guid EducationMediumStageId { get; set; }
		public string Name { get; set; } = string.Empty;
	}
}
