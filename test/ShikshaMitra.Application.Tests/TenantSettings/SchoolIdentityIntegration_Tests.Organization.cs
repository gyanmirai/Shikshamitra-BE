using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.SchoolIdentity;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.TenantSettings;

public partial class SchoolIdentityIntegration_Tests
{
	[Fact]
	public Task Organization_legacy_banner_import_is_idempotent_and_preserves_original_bytes() => RunAsync(async (board, medium) =>
	{
		var stage = await GetRequiredService<IRepository<EducationMediumStage, Guid>>().InsertAsync(new(Guid.NewGuid(), TenantId, "Legacy", Png, medium), true);
		var importer = GetRequiredService<OrganizationIdentityLegacyMigrationContributor>();
		await importer.SeedAsync(new DataSeedContext(TenantId));
		var imported = await Service.GetAsync(new() { BoardId = board, MediumId = medium, OrganizationId = stage.Id });
		imported.Effective["banner"].Source.ShouldBe("Organization override");
		(await Service.DownloadAsync(imported.Choices.Banner.AssetId!.Value, new() { BoardId = board, MediumId = medium, OrganizationId = stage.Id })).Content.ShouldBe(Png);
		await Service.UpdateAsync(new() { BoardId = board, MediumId = medium, OrganizationId = stage.Id,
			ConcurrencyStamp = imported.ConcurrencyStamp, Choices = new() { Banner = new() { Mode = SchoolIdentityAssetMode.Hidden } } });
		await importer.SeedAsync(new DataSeedContext(TenantId));
		(await Service.GetAsync(new() { BoardId = board, MediumId = medium, OrganizationId = stage.Id })).Choices.Banner.Mode.ShouldBe(SchoolIdentityAssetMode.Hidden);
		(await GetRequiredService<IRepository<EducationMediumStage, Guid>>().GetAsync(stage.Id)).BannerPicture.ShouldBe(Png);
	});

	[Fact]
	public Task Organization_images_are_independent_inherit_reset_and_freeze_in_documents() => RunAsync(async (board, medium) =>
	{
		var stages = GetRequiredService<IRepository<EducationMediumStage, Guid>>();
		var first = await stages.InsertAsync(new(Guid.NewGuid(), TenantId, "Primary", null, medium), true);
		var second = await stages.InsertAsync(new(Guid.NewGuid(), TenantId, "Secondary", null, medium), true);
		var parent = await Service.UploadAsync(SchoolIdentityAssetKind.Banner, board, medium, "medium.png", Png);
		await Service.UpdateAsync(new() { BoardId = board, MediumId = medium, Choices = new() { Banner = Custom(parent.Id) } });
		var banner = await Service.UploadAsync(SchoolIdentityAssetKind.Banner, board, medium, "primary.png", Png, first.Id);
		var saved = await Service.UpdateAsync(new() { BoardId = board, MediumId = medium, OrganizationId = first.Id,
			Choices = new() { Banner = Custom(banner.Id) } });
		saved.OrganizationId.ShouldBe(first.Id);
		saved.Effective["banner"].Source.ShouldBe("Organization override");
		(await Service.GetAsync(new() { BoardId = board, MediumId = medium, OrganizationId = second.Id }))
			.Effective["banner"].Asset!.Id.ShouldBe(parent.Id);
		var documents = GetRequiredService<SchoolIdentityDocumentService>();
		var snapshot = await documents.CaptureForStageAsync(first.Id);
		snapshot.OrganizationId.ShouldBe(first.Id);
		snapshot.BannerAssetId.ShouldBe(banner.Id);
		var preview = await Service.PreviewAsync(new() { BoardId = board, MediumId = medium, OrganizationId = first.Id });
		preview.Content.Take(4).ShouldBe("%PDF"u8.ToArray());
		await Service.UpdateAsync(new() { BoardId = board, MediumId = medium, OrganizationId = first.Id, ConcurrencyStamp = saved.ConcurrencyStamp });
		(await Service.GetAsync(new() { BoardId = board, MediumId = medium, OrganizationId = first.Id }))
			.Effective["banner"].Asset!.Id.ShouldBe(parent.Id);
		(await documents.LoadAsync(snapshot)).Banner.ShouldBe(Png);
		(await Service.GetHistoryAsync(new() { BoardId = board, MediumId = medium, OrganizationId = first.Id })).Count.ShouldBe(2);
		await Should.ThrowAsync<BusinessException>(() => Service.GetAsync(new() { BoardId = board, OrganizationId = first.Id }));
		var otherMedium = await GetRequiredService<IRepository<EducationMedium, Guid>>().InsertAsync(new(Guid.NewGuid(), TenantId, "Other", board), true);
		await Should.ThrowAsync<BusinessException>(() => Service.GetAsync(new() { BoardId = board, MediumId = otherMedium.Id, OrganizationId = first.Id }));
	});

	[Fact]
	public Task Organization_scope_does_not_grant_sibling_or_parent_management_or_asset_access() => RunAsync(async (board, medium) =>
	{
		var stages = GetRequiredService<IRepository<EducationMediumStage, Guid>>();
		var first = await stages.InsertAsync(new(Guid.NewGuid(), TenantId, "Primary", null, medium), true);
		var sibling = await stages.InsertAsync(new(Guid.NewGuid(), TenantId, "Secondary", null, medium), true);
		var banner = await Service.UploadAsync(SchoolIdentityAssetKind.Banner, board, medium, "sibling.png", Png, sibling.Id);
		await Service.UpdateAsync(new() { BoardId = board, MediumId = medium, OrganizationId = sibling.Id, Choices = new() { Banner = Custom(banner.Id) } });
		var year = (await GetRequiredService<IRepository<EducationBoard, Guid>>().GetAsync(board)).AcademicYearId;
		var actorId = Guid.NewGuid();
		await GetRequiredService<IRepository<IdentityUser, Guid>>().InsertAsync(new(actorId, $"organization-{actorId:N}", $"{actorId:N}@example.test", TenantId), true);
		await GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(new(Guid.NewGuid(), TenantId, userId: actorId,
			academicYearId: year, educationBoardId: board, educationMediumId: medium, educationMediumStageId: first.Id), true);
		using var actor = GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity([
			new Claim(AbpClaimTypes.UserId, actorId.ToString()), new Claim(AbpClaimTypes.TenantId, TenantId.ToString())], "Test")));
		ScopeLocalReuseInvalidator.ClearCurrentUnitOfWork(GetRequiredService<IUnitOfWorkManager>());
		(await Service.GetAsync(new() { BoardId = board, MediumId = medium, OrganizationId = first.Id })).CanManage.ShouldBeTrue();
		await Service.UpdateAsync(new() { BoardId = board, MediumId = medium, OrganizationId = first.Id, Choices = new() { Banner = new() { Mode = SchoolIdentityAssetMode.Hidden } } });
		await Should.ThrowAsync<BusinessException>(() => Service.GetAsync(new() { BoardId = board, MediumId = medium, OrganizationId = sibling.Id }));
		await Should.ThrowAsync<BusinessException>(() => Service.UpdateAsync(new() { BoardId = board, MediumId = medium }));
		await Should.ThrowAsync<BusinessException>(() => Service.DownloadAsync(banner.Id, new() { BoardId = board, MediumId = medium, OrganizationId = first.Id }));
	});
}
