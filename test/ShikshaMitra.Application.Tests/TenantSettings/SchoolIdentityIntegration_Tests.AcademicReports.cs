using System;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Reports;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.SchoolIdentity;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.TenantSettings;

public partial class SchoolIdentityIntegration_Tests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public Task Academic_pdf_uses_organization_banner_even_with_explicit_board_and_medium(bool explicitContext) => RunAsync(async (board, medium) =>
	{
		var stage = await GetRequiredService<IRepository<EducationMediumStage, Guid>>().InsertAsync(new(Guid.NewGuid(), TenantId, "Primary", null, medium), true);
		var standard = await GetRequiredService<IRepository<Standard, Guid>>().InsertAsync(new(Guid.NewGuid(), TenantId, "Class 5", 8, stage.Id, null), true);
		var parent = await Service.UploadAsync(SchoolIdentityAssetKind.Banner, board, medium, "medium.png", Png);
		await Service.UpdateAsync(new() { BoardId = board, MediumId = medium, Choices = new() { Banner = Custom(parent.Id) } });
		var organizationImage = SchoolIdentityTestImage.Png(1500, 320);
		var banner = await Service.UploadAsync(SchoolIdentityAssetKind.Banner, board, medium, "primary.png", organizationImage, stage.Id);
		await Service.UpdateAsync(new() { BoardId = board, MediumId = medium, OrganizationId = stage.Id, Choices = new() { Banner = Custom(banner.Id) } });
		var report = new AcademicReportPreviewDto { NormalizedFilters = new() { ClassIds = [standard.Id] } };
		await GetRequiredService<AcademicReportIdentityService>().PrepareAsync(report, explicitContext ? board : null, explicitContext ? medium : null);
		report.IdentityBanner.ShouldBe(organizationImage);
	});

	[Fact]
	public Task Academic_pdf_division_only_filter_uses_organization_banner() => RunAsync(async (board, medium) =>
	{
		var stage = await GetRequiredService<IRepository<EducationMediumStage, Guid>>().InsertAsync(new(Guid.NewGuid(), TenantId, "Primary", null, medium), true);
		var standard = await GetRequiredService<IRepository<Standard, Guid>>().InsertAsync(new(Guid.NewGuid(), TenantId, "Class 5", 8, stage.Id, null), true);
		var division = await GetRequiredService<IRepository<Division, Guid>>().InsertAsync(new(Guid.NewGuid(), TenantId, "A", standard.Id, 30, ""), true);
		var banner = await Service.UploadAsync(SchoolIdentityAssetKind.Banner, board, medium, "primary.png", Png, stage.Id);
		await Service.UpdateAsync(new() { BoardId = board, MediumId = medium, OrganizationId = stage.Id, Choices = new() { Banner = Custom(banner.Id) } });
		var report = new AcademicReportPreviewDto { NormalizedFilters = new() { DivisionIds = [division.Id] } };
		await GetRequiredService<AcademicReportIdentityService>().PrepareAsync(report);
		report.IdentityBanner.ShouldBe(Png);
	});

	[Theory]
	[InlineData("medium")]
	[InlineData("board")]
	[InlineData("school")]
	public Task Academic_pdf_uses_nearest_shared_branding_for_multiple_organizations(string sharedLevel) => RunAsync(async (board, medium) =>
	{
		var banner = await Service.UploadAsync(SchoolIdentityAssetKind.Banner,
			sharedLevel == "school" ? null : board, sharedLevel == "medium" ? medium : null, "shared.png", Png);
		await Service.UpdateAsync(new() { BoardId = sharedLevel == "school" ? null : board,
			MediumId = sharedLevel == "medium" ? medium : null, Choices = new() { Banner = Custom(banner.Id) } });
		var classes = new System.Collections.Generic.List<Guid>();
		for (var index = 0; index < 2; index++)
		{
			var classBoard = board;
			if (index == 1 && sharedLevel == "school")
			{
				var original = await GetRequiredService<IRepository<EducationBoard, Guid>>().GetAsync(board);
				classBoard = (await GetRequiredService<IRepository<EducationBoard, Guid>>().InsertAsync(new(Guid.NewGuid(), TenantId,
					ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards.EducationBoardType.CBSE, original.AcademicYearId), true)).Id;
			}
			var classMedium = medium;
			if (index == 1 && sharedLevel != "medium")
				classMedium = (await GetRequiredService<IRepository<EducationMedium, Guid>>().InsertAsync(new(Guid.NewGuid(), TenantId, "English", classBoard), true)).Id;
			var stage = await GetRequiredService<IRepository<EducationMediumStage, Guid>>().InsertAsync(new(Guid.NewGuid(), TenantId, $"Organization {index}", null, classMedium), true);
			var standard = await GetRequiredService<IRepository<Standard, Guid>>().InsertAsync(new(Guid.NewGuid(), TenantId, $"Class {index}", 8, stage.Id, null), true);
			classes.Add(standard.Id);
			var own = await Service.UploadAsync(SchoolIdentityAssetKind.Banner, classBoard, classMedium, "own.png", SchoolIdentityTestImage.Png(1600, 350), stage.Id);
			await Service.UpdateAsync(new() { BoardId = classBoard, MediumId = classMedium, OrganizationId = stage.Id, Choices = new() { Banner = Custom(own.Id) } });
		}
		var report = new AcademicReportPreviewDto { NormalizedFilters = new() { ClassIds = classes } };
		await GetRequiredService<AcademicReportIdentityService>().PrepareAsync(report);
		report.IdentityBanner.ShouldBe(Png);
	});

	[Fact]
	public Task Academic_pdf_honours_hidden_banner_and_inherits_logo() => RunAsync(async (board, medium) =>
	{
		var logo = await Service.UploadAsync(SchoolIdentityAssetKind.Logo, null, null, "logo.png", Png);
		var banner = await Service.UploadAsync(SchoolIdentityAssetKind.Banner, null, null, "banner.png", Png);
		await Service.UpdateAsync(new() { Choices = new() { Logo = Custom(logo.Id), Banner = Custom(banner.Id) } });
		var stage = await GetRequiredService<IRepository<EducationMediumStage, Guid>>().InsertAsync(new(Guid.NewGuid(), TenantId, "Primary", null, medium), true);
		var standard = await GetRequiredService<IRepository<Standard, Guid>>().InsertAsync(new(Guid.NewGuid(), TenantId, "Class 5", 8, stage.Id, null), true);
		await Service.UpdateAsync(new() { BoardId = board, MediumId = medium, OrganizationId = stage.Id,
			Choices = new() { Banner = new() { Mode = SchoolIdentityAssetMode.Hidden } } });
		var report = new AcademicReportPreviewDto { NormalizedFilters = new() { ClassIds = [standard.Id] } };
		await GetRequiredService<AcademicReportIdentityService>().PrepareAsync(report, board, medium);
		report.IdentityBanner.ShouldBeNull();
		report.IdentityLogo.ShouldBe(Png);
	});

	[Fact]
	public Task Academic_pdf_rejects_class_from_another_explicit_medium() => RunAsync(async (board, medium) =>
	{
		var other = await GetRequiredService<IRepository<EducationMedium, Guid>>().InsertAsync(new(Guid.NewGuid(), TenantId, "English", board), true);
		var stage = await GetRequiredService<IRepository<EducationMediumStage, Guid>>().InsertAsync(new(Guid.NewGuid(), TenantId, "Primary", null, other.Id), true);
		var standard = await GetRequiredService<IRepository<Standard, Guid>>().InsertAsync(new(Guid.NewGuid(), TenantId, "Class 5", 8, stage.Id, null), true);
		var report = new AcademicReportPreviewDto { NormalizedFilters = new() { ClassIds = [standard.Id] } };
		(await Should.ThrowAsync<BusinessException>(() => GetRequiredService<AcademicReportIdentityService>().PrepareAsync(report, board, medium)))
			.Code.ShouldBe(SchoolIdentityErrorCodes.InvalidContext);
	});

	[Fact]
	public Task Academic_pdf_cannot_use_another_tenants_class_or_branding() => RunAsync(async (board, medium) =>
	{
		var stage = await GetRequiredService<IRepository<EducationMediumStage, Guid>>().InsertAsync(new(Guid.NewGuid(), TenantId, "Primary", null, medium), true);
		var standard = await GetRequiredService<IRepository<Standard, Guid>>().InsertAsync(new(Guid.NewGuid(), TenantId, "Class 5", 8, stage.Id, null), true);
		var report = new AcademicReportPreviewDto { NormalizedFilters = new() { ClassIds = [standard.Id] } };
		using var tenant = GetRequiredService<Volo.Abp.MultiTenancy.ICurrentTenant>().Change(Guid.NewGuid());
		(await Should.ThrowAsync<BusinessException>(() => GetRequiredService<AcademicReportIdentityService>().PrepareAsync(report)))
			.Code.ShouldBe(SchoolIdentityErrorCodes.InvalidContext);
		report.IdentityBanner.ShouldBeNull();
		report.IdentityLogo.ShouldBeNull();
	});

	[Fact]
	public Task Academic_pdf_without_configured_assets_remains_unbranded() => RunAsync(async (_, _) =>
	{
		var report = new AcademicReportPreviewDto();
		await GetRequiredService<AcademicReportIdentityService>().PrepareAsync(report);
		report.IdentityBanner.ShouldBeNull();
		report.IdentityLogo.ShouldBeNull();
	});
}
