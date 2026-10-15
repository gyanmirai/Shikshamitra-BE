using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.SchoolIdentity;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.TenantSettings;

public partial class SchoolIdentityIntegration_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
	private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aRZkAAAAASUVORK5CYII=");
	private ISchoolIdentityAppService Service => GetRequiredService<ISchoolIdentityAppService>();

	[Fact]
	public Task Saving_partial_overrides_resolves_real_assets_and_reset_restores_inheritance() => RunAsync(async (board, medium) =>
	{
		var logo = await Service.UploadAsync(SchoolIdentityAssetKind.Logo, null, null, "logo.png", Png);
		var defaults = await Service.UpdateAsync(new() { Choices = new() { Logo = Custom(logo.Id) } });
		var banner = await Service.UploadAsync(SchoolIdentityAssetKind.Banner, board, null, "banner.png", Png);
		await Service.UpdateAsync(new() { BoardId = board, Choices = new() { Banner = Custom(banner.Id) } });
		var selected = await Service.GetAsync(new() { BoardId = board, MediumId = medium });
		selected.Effective["logo"].Asset!.Id.ShouldBe(logo.Id);
		selected.Effective["banner"].Asset!.Id.ShouldBe(banner.Id);
		selected.Effective["logo"].Source.ShouldBe("School default");
		selected = await Service.UpdateAsync(new() { BoardId = board, MediumId = medium, Choices = new() { Logo = new() { Mode = SchoolIdentityAssetMode.Hidden } } });
		selected.Effective["logo"].Asset.ShouldBeNull();
		selected = await Service.UpdateAsync(new() { BoardId = board, MediumId = medium, ConcurrencyStamp = selected.ConcurrencyStamp });
		selected.Effective["logo"].Asset!.Id.ShouldBe(logo.Id);
		(await Service.GetHistoryAsync(new() { BoardId = board, MediumId = medium })).Count.ShouldBe(2);
		(await Service.DownloadAsync(logo.Id, new() { BoardId = board, MediumId = medium })).Content.ShouldBe(Png);
		await Should.ThrowAsync<AbpDbConcurrencyException>(() => Service.UpdateAsync(new() { ConcurrencyStamp = defaults.ConcurrencyStamp + "stale" }));
	});

	[Fact]
	public Task Document_snapshots_keep_original_assets_after_default_replacement() => RunAsync(async (board, medium) =>
	{
		var original = await Service.UploadAsync(SchoolIdentityAssetKind.Logo, null, null, "original.png", Png);
		var saved = await Service.UpdateAsync(new() { Choices = new() { Logo = Custom(original.Id) } });
		var documents = GetRequiredService<SchoolIdentityDocumentService>();
		var snapshot = await documents.CaptureAsync(board, medium, null, signature: false);
		var replacement = await Service.UploadAsync(SchoolIdentityAssetKind.Logo, null, null, "replacement.png", Png);
		await Service.UpdateAsync(new() { ConcurrencyStamp = saved.ConcurrencyStamp, Choices = new() { Logo = Custom(replacement.Id) } });
		snapshot.LogoAssetId.ShouldBe(original.Id);
		(await documents.LoadAsync(snapshot)).Logo.ShouldBe(Png);
		(await Service.GetAsync(new())).Effective["logo"].Asset!.Id.ShouldBe(replacement.Id);
		var asset = await GetRequiredService<IRepository<SchoolIdentityAsset, Guid>>().GetAsync(original.Id);
		asset.IsRetained.ShouldBeTrue();
	});

	[Fact]
	public Task Another_tenant_cannot_read_or_reference_an_asset_and_bad_medium_parent_is_rejected() => RunAsync(async (board, medium) =>
	{
		var logo = await Service.UploadAsync(SchoolIdentityAssetKind.Logo, null, null, "private.png", Png);
		using (GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid()))
			await Should.ThrowAsync<EntityNotFoundException>(() => Service.DownloadAsync(logo.Id, new()));
		var boards = GetRequiredService<IRepository<EducationBoard, Guid>>();
		var original = await boards.GetAsync(board);
		var another = await boards.InsertAsync(new(Guid.NewGuid(), TenantId, EducationBoardType.CBSE, original.AcademicYearId), true);
		await Should.ThrowAsync<BusinessException>(() => Service.GetAsync(new() { BoardId = another.Id, MediumId = medium }));
	});

	[Fact]
	public Task A_claim_only_admin_cannot_manage_school_defaults_or_unassigned_board() => RunAsync(async (board, medium) =>
	{
		var users = GetRequiredService<IRepository<IdentityUser, Guid>>();
		var actor = Guid.Parse(GetRequiredService<ICurrentPrincipalAccessor>().Principal.FindFirst(AbpClaimTypes.UserId)!.Value);
		var user = await users.GetAsync(actor);
		foreach (var role in user.Roles.ToList()) user.RemoveRole(role.RoleId);
		await users.UpdateAsync(user, true);
		ShikshaMitra.Security.UserScope.ScopeLocalReuseInvalidator.ClearCurrentUnitOfWork(GetRequiredService<Volo.Abp.Uow.IUnitOfWorkManager>());
		await Should.ThrowAsync<BusinessException>(() => Service.UpdateAsync(new()));
		await Should.ThrowAsync<BusinessException>(() => Service.GetAsync(new() { BoardId = board, MediumId = medium }));
	});

	private Task RunAsync(Func<Guid, Guid, Task> scenario)
	{
		var currentTenant = GetRequiredService<ICurrentTenant>();
		var principal = GetRequiredService<ICurrentPrincipalAccessor>();
		return WithUnitOfWorkAsync(async () =>
		{
			using var tenant = currentTenant.Change(TenantId);
			var userId = Guid.NewGuid();
			using var actor = principal.Change(new ClaimsPrincipal(new ClaimsIdentity([
				new Claim(AbpClaimTypes.UserId, userId.ToString()), new Claim(AbpClaimTypes.UserName, $"identity-{userId:N}"),
				new Claim(AbpClaimTypes.Role, "admin"), new Claim(AbpClaimTypes.TenantId, TenantId.ToString())], "Test")));
			var roles = GetRequiredService<IRepository<IdentityRole, Guid>>();
			var role = (await roles.GetListAsync(x => x.Name == "admin")).FirstOrDefault()
				?? await roles.InsertAsync(new(Guid.NewGuid(), "admin", TenantId), true);
			var user = new IdentityUser(userId, $"identity-{userId:N}", $"{userId:N}@example.test", TenantId); user.AddRole(role.Id);
			await GetRequiredService<IRepository<IdentityUser, Guid>>().InsertAsync(user, true);
			var year = await GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(new(Guid.NewGuid(), TenantId, "Identity QA year", new DateTime(2026, 4, 1), new DateTime(2027, 3, 31)), true);
			var board = await GetRequiredService<IRepository<EducationBoard, Guid>>().InsertAsync(new(Guid.NewGuid(), TenantId, EducationBoardType.GSEB, year.Id), true);
			var medium = await GetRequiredService<IRepository<EducationMedium, Guid>>().InsertAsync(new(Guid.NewGuid(), TenantId, "Gujarati", board.Id), true);
			await scenario(board.Id, medium.Id);
		});
	}
	private static SchoolIdentitySelectionDto Custom(Guid id) => new() { Mode = SchoolIdentityAssetMode.Custom, AssetId = id };
}
