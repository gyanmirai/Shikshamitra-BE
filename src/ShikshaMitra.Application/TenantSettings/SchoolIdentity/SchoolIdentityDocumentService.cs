using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.EducationBoards;

namespace ShikshaMitra.TenantSettings.SchoolIdentity;

public sealed record SchoolIdentityDocumentAssets(byte[]? Logo, byte[]? Banner, byte[]? Signature,
	string? PrincipalName, string? Designation);

[RemoteService(false)]
public class SchoolIdentityDocumentService(SchoolIdentityReadService reader,
	IRepository<SchoolIdentityAsset, Guid> assets, IBlobContainer blobs,
	IRepository<EducationMediumStage, Guid> stages, IRepository<EducationMedium, Guid> mediums,
	IRepository<EducationBoard, Guid> boards) : ApplicationService, ITransientDependency
{
	// Callers enforce their own document permission and student/employee scope before capturing identity.
	public async Task<SchoolIdentityDocumentSnapshotDto> CaptureAsync(Guid? boardId, Guid? mediumId, Guid? yearId, bool signature)
	{
		var profile = await reader.ReadAsync(boardId, mediumId, yearId, signature);
		return Snapshot(profile, CurrentTenant.Id!.Value);
	}

	public async Task<SchoolIdentityDocumentSnapshotDto> CaptureForStageAsync(Guid? stageId, bool signature = false)
	{
		if (!stageId.HasValue) return await CaptureAsync(null, null, null, signature);
		var stage = await stages.GetAsync(stageId.Value);
		var medium = await mediums.GetAsync(stage.EducationMediumId);
		var board = await boards.GetAsync(medium.EducationBoardId);
		if (stage.TenantId != CurrentTenant.Id || medium.TenantId != CurrentTenant.Id || board.TenantId != CurrentTenant.Id)
			throw new BusinessException(SchoolIdentityErrorCodes.InvalidContext);
		return await CaptureAsync(board.Id, medium.Id, board.AcademicYearId, signature);
	}

	public static SchoolIdentityDocumentSnapshotDto Snapshot(SchoolIdentityProfileDto profile, Guid tenantId) => new()
	{
		TenantId = tenantId, BoardId = profile.BoardId, MediumId = profile.MediumId,
		LogoAssetId = profile.Effective.GetValueOrDefault("logo")?.Selection.AssetId,
		BannerAssetId = profile.Effective.GetValueOrDefault("banner")?.Selection.AssetId,
		SignatureAssetId = profile.Effective.GetValueOrDefault("signature")?.Selection.AssetId,
		PrincipalName = profile.Effective.GetValueOrDefault("signature")?.Selection.PrincipalName,
		Designation = profile.Effective.GetValueOrDefault("signature")?.Selection.Designation
	};

	public async Task<SchoolIdentityDocumentAssets> LoadAsync(SchoolIdentityDocumentSnapshotDto? snapshot)
	{
		if (snapshot == null) return new(null, null, null, null, null);
		if (snapshot.TenantId != CurrentTenant.Id) throw new BusinessException(SchoolIdentityErrorCodes.InvalidContext);
		return new(await BytesAsync(snapshot.LogoAssetId, SchoolIdentityAssetKind.Logo),
			await BytesAsync(snapshot.BannerAssetId, SchoolIdentityAssetKind.Banner),
			await BytesAsync(snapshot.SignatureAssetId, SchoolIdentityAssetKind.Signature), snapshot.PrincipalName, snapshot.Designation);
	}

	private async Task<byte[]?> BytesAsync(Guid? id, SchoolIdentityAssetKind kind)
	{
		if (!id.HasValue) return null;
		var asset = await assets.GetAsync(id.Value);
		if (asset.TenantId != CurrentTenant.Id || asset.Kind != kind) throw new BusinessException(SchoolIdentityErrorCodes.AssetInvalid);
		return await blobs.GetAllBytesAsync(asset.BlobName);
	}
}
