using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Linq;
using Volo.Abp.Uow;

namespace ShikshaMitra.TenantSettings.SchoolIdentity;

public class OrganizationIdentityLegacyMigrationContributor(
	ICurrentTenant currentTenant, IRepository<EducationMediumStage, Guid> stages,
	IRepository<EducationMedium, Guid> mediums, IRepository<EducationBoard, Guid> boards,
	IRepository<SchoolIdentityProfile, Guid> profiles, IRepository<SchoolIdentityAsset, Guid> assets,
	IRepository<SchoolIdentityRevision, Guid> revisions, IBlobContainer blobs, IGuidGenerator guids,
	ILogger<OrganizationIdentityLegacyMigrationContributor> logger, IAsyncQueryableExecuter executor) : IDataSeedContributor, ITransientDependency
{
	[UnitOfWork]
	public virtual async Task SeedAsync(DataSeedContext context)
	{
		if (!context.TenantId.HasValue) return;
		using var tenantScope = currentTenant.Change(context.TenantId);
		var stageQuery = await stages.GetQueryableAsync();
		var mediumQuery = await mediums.GetQueryableAsync();
		var boardQuery = await boards.GetQueryableAsync();
		var candidates = from stage in stageQuery
			join medium in mediumQuery on stage.EducationMediumId equals medium.Id
			join board in boardQuery on medium.EducationBoardId equals board.Id
			where stage.TenantId == context.TenantId && medium.TenantId == context.TenantId
				&& board.TenantId == context.TenantId && stage.BannerPicture != null
			select new { Stage = stage, Medium = medium, Board = board };
		foreach (var item in await executor.ToListAsync(candidates))
		{
			var key = SchoolIdentityProfile.Key(item.Board.Id, item.Medium.Id, item.Stage.Id);
			if (await profiles.AnyAsync(x => x.TenantId == context.TenantId && x.ScopeKey == key)) continue;
			var bytes = item.Stage.BannerPicture!;
			(string ContentType, int Width, int Height) image;
			try { image = SchoolIdentityImageValidation.ValidateLegacy(SchoolIdentityAssetKind.Banner, bytes); }
			catch (BusinessException)
			{
				logger.LogWarning("Organization {OrganizationId} banner could not be imported. Its original bytes are preserved.", item.Stage.Id);
				continue;
			}
			var id = guids.Create();
			var blobName = $"school-identity/{context.TenantId:N}/{id:N}";
			await blobs.SaveAsync(blobName, bytes, overrideExisting: false);
			var asset = new SchoolIdentityAsset(id, context.TenantId.Value, SchoolIdentityAssetKind.Banner, blobName,
				$"existing-organization-banner.{(image.ContentType == "image/png" ? "png" : "jpg")}",
				image.ContentType, bytes.LongLength, image.Width, image.Height, item.Board.Id, item.Medium.Id, item.Stage.Id);
			asset.Retain();
			await assets.InsertAsync(asset);
			var profile = new SchoolIdentityProfile(guids.Create(), context.TenantId.Value,
				item.Board.AcademicYearId, item.Board.Id, item.Medium.Id, item.Stage.Id);
			profile.Replace(new() { Banner = new() { Mode = SchoolIdentityAssetMode.Custom, AssetId = id } }, profile.ConcurrencyStamp);
			await profiles.InsertAsync(profile, autoSave: true);
			await revisions.InsertAsync(new(guids.Create(), profile, "Imported existing organization banner"));
		}
	}
}
