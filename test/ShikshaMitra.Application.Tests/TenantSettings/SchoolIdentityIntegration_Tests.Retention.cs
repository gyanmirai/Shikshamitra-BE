using System;
using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.SchoolIdentity;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentRecords;

namespace ShikshaMitra.TenantSettings;

public partial class SchoolIdentityIntegration_Tests
{
	[Fact]
	public Task Editing_one_certificate_asset_preserves_legacy_custom_values_and_other_choices() => RunAsync(async (_, _) =>
	{
		var service = GetRequiredService<IStudentCertificateAppService>();
		var template = await service.UpsertTemplateAsync(StudentCertificateType.Bonafide, new()
		{
			HeaderContent = "Configured heading", FooterContent = "Configured footer", LogoUrl = "/api/files/existing-logo.png",
			PrincipalSignatureUrl = "/api/files/existing-signature.png", IsActive = true
		});
		template.IdentityChoices = new() { Banner = new() { Mode = SchoolIdentityAssetMode.Hidden } };
		var updated = await service.UpsertTemplateAsync(StudentCertificateType.Bonafide, template);
		updated.LogoUrl.ShouldBe(template.LogoUrl); updated.PrincipalSignatureUrl.ShouldBe(template.PrincipalSignatureUrl);
		updated.IdentityChoices!.Logo.ShouldBeNull(); updated.IdentityChoices.Signature.ShouldBeNull();
		updated.IdentityChoices.Banner!.Mode.ShouldBe(SchoolIdentityAssetMode.Hidden);
	});
	[Fact]
	public Task Expired_draft_cleanup_never_claims_saved_assets_and_respects_the_age_cutoff() => RunAsync(async (_, _) =>
	{
		var saved = await Service.UploadAsync(SchoolIdentityAssetKind.Logo, null, null, "retained.png", Png);
		await Service.UpdateAsync(new() { Choices = new() { Logo = Custom(saved.Id) } });
		var draft = await Service.UploadAsync(SchoolIdentityAssetKind.Banner, null, null, "draft.png", Png);
		var repository = GetRequiredService<ISchoolIdentityAssetRepository>();
		(await repository.TryClaimExpiredDraftAsync(saved.Id, DateTime.UtcNow.AddDays(1))).ShouldBeFalse();
		(await repository.TryClaimExpiredDraftAsync(draft.Id, DateTime.UtcNow.AddDays(-1))).ShouldBeFalse();
		(await repository.TryClaimExpiredDraftAsync(draft.Id, DateTime.UtcNow.AddDays(1))).ShouldBeTrue();
		(await repository.TryClaimExpiredDraftAsync(draft.Id, DateTime.UtcNow.AddDays(1))).ShouldBeFalse();
		await repository.CompleteDraftCleanupAsync(draft.Id);
		(await repository.AnyAsync(x => x.Id == draft.Id)).ShouldBeFalse();
		(await repository.AnyAsync(x => x.Id == saved.Id && x.IsRetained)).ShouldBeTrue();
	});

	[Fact]
	public Task Copying_to_another_year_preserves_explicit_choices_without_duplicating_files() => RunAsync(async (board, medium) =>
	{
		var logo = await Service.UploadAsync(SchoolIdentityAssetKind.Logo, board, medium, "medium.png", Png);
		await Service.UpdateAsync(new() { BoardId = board, MediumId = medium,
			Choices = new() { Logo = Custom(logo.Id), Banner = new() { Mode = SchoolIdentityAssetMode.Hidden } } });
		var years = GetRequiredService<IRepository<AcademicYear, Guid>>();
		var nextYear = await years.InsertAsync(new(Guid.NewGuid(), TenantId, "Identity next year", new DateTime(2027, 4, 1), new DateTime(2028, 3, 31)), true);
		var nextBoard = await GetRequiredService<IRepository<EducationBoard, Guid>>().InsertAsync(new(Guid.NewGuid(), TenantId, EducationBoardType.GSEB, nextYear.Id), true);
		var nextMedium = await GetRequiredService<IRepository<EducationMedium, Guid>>().InsertAsync(new(Guid.NewGuid(), TenantId, "Gujarati", nextBoard.Id), true);
		var before = await GetRequiredService<IRepository<SchoolIdentityAsset, Guid>>().GetCountAsync();
		var copied = await Service.CopyAsync(new() { SourceBoardId = board, SourceMediumId = medium, TargetBoardId = nextBoard.Id, TargetMediumId = nextMedium.Id });
		copied.Choices.Logo.AssetId.ShouldBe(logo.Id);
		copied.Choices.Banner.Mode.ShouldBe(SchoolIdentityAssetMode.Hidden);
		(await GetRequiredService<IRepository<SchoolIdentityAsset, Guid>>().GetCountAsync()).ShouldBe(before);
		await Should.ThrowAsync<BusinessException>(() => Service.CopyAsync(new() { SourceBoardId = board, TargetBoardId = board }));
	});
}
