using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.BlobStoring;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace ShikshaMitra.TenantSettings.SchoolIdentity;

[Authorize]
[RemoteService(false)]
public partial class SchoolIdentityAppService(
	IRepository<SchoolIdentityProfile, Guid> profiles,
	IRepository<SchoolIdentityAsset, Guid> assets,
	IRepository<SchoolIdentityRevision, Guid> revisions,
	IBlobContainer blobs,
	IPermissionChecker permissionChecker,
	SchoolIdentityScopeAccess access,
	SchoolIdentityReadService reader,
	SchoolIdentityDocumentService documents,
	IRepository<IdentityUser, Guid> identityUsers) : ApplicationService, ISchoolIdentityAppService
{
	public async Task<SchoolIdentityProfileDto> GetAsync(SchoolIdentityContextDto input)
	{
		await AuthorizationService.CheckAsync(SchoolIdentityPermissions.View);
		var year = await access.EnsureAsync(input.BoardId, input.MediumId, organizationId: input.OrganizationId);
		var result = await reader.ReadAsync(input.BoardId, input.MediumId, year,
			await permissionChecker.IsGrantedAsync(SchoolIdentityPermissions.AccessSignature), organizationId: input.OrganizationId);
		result.CanManage = await permissionChecker.IsGrantedAsync(input.BoardId.HasValue
			? SchoolIdentityPermissions.ManageOverrides : SchoolIdentityPermissions.ManageDefaults)
			&& await access.CanManageAsync(input.BoardId, input.MediumId, year, input.OrganizationId);
		return result;
	}

	public async Task<SchoolIdentityProfileDto> UpdateAsync(SchoolIdentityUpdateDto input)
	{
		await AuthorizationService.CheckAsync(SchoolIdentityPermissions.View);
		await AuthorizationService.CheckAsync(input.BoardId.HasValue ? SchoolIdentityPermissions.ManageOverrides : SchoolIdentityPermissions.ManageDefaults);
		if (input.Choices == null) throw new BusinessException(SchoolIdentityErrorCodes.InvalidSelection);
		var year = await access.EnsureAsync(input.BoardId, input.MediumId, manage: true, organizationId: input.OrganizationId);
		var profile = await reader.FindAsync(input.BoardId, input.MediumId, input.OrganizationId);
		if (profile == null && input.ConcurrencyStamp != "") throw new AbpDbConcurrencyException();
		if (profile != null && profile.ConcurrencyStamp != input.ConcurrencyStamp) throw new AbpDbConcurrencyException();
		var original = profile?.Choices ?? new();
		if (input.Choices.Signature != null) await AuthorizationService.CheckAsync(SchoolIdentityPermissions.AccessSignature);
		var choices = await PrepareChoicesAsync(input.Choices, original);
		await RetainAssetsAsync(choices, original);
		if (profile == null)
		{
			profile = new(GuidGenerator.Create(), CurrentTenant.Id!.Value, year, input.BoardId, input.MediumId, input.OrganizationId);
			profile.Replace(choices, profile.ConcurrencyStamp);
			await profiles.InsertAsync(profile, autoSave: true);
		}
		else
		{
			profile.Replace(choices, input.ConcurrencyStamp);
			await profiles.UpdateAsync(profile, autoSave: true);
		}
		await revisions.InsertAsync(new(GuidGenerator.Create(), profile, "Updated school identity"));
		return await GetAsync(new() { BoardId = input.BoardId, MediumId = input.MediumId, OrganizationId = input.OrganizationId });
	}

	private async Task RetainAssetsAsync(SchoolIdentityChoices choices, SchoolIdentityChoices original, bool retain = true)
	{
		foreach (var kind in Enum.GetValues<SchoolIdentityAssetKind>())
		{
			var selection = choices.Get(kind);
			if (selection.Mode != SchoolIdentityAssetMode.Custom) continue;
			var asset = await assets.GetAsync(selection.AssetId!.Value);
			if (asset.TenantId != CurrentTenant.Id || asset.Kind != kind) throw new BusinessException(SchoolIdentityErrorCodes.AssetInvalid);
			if (!asset.IsRetained && asset.CreatorId != CurrentUser.Id) throw new AbpAuthorizationException();
			if (original.Get(kind).AssetId != asset.Id)
				await access.EnsureAsync(asset.EducationBoardId, asset.EducationMediumId, manage: !asset.IsRetained, organizationId: asset.OrganizationId);
			if (retain)
			{
				asset.Retain();
				await assets.UpdateAsync(asset);
			}
		}
	}

	public async Task<SchoolIdentityProfileDto> CopyAsync(SchoolIdentityCopyDto input)
	{
		await AuthorizationService.CheckAsync(SchoolIdentityPermissions.View);
		await AuthorizationService.CheckAsync(SchoolIdentityPermissions.ManageOverrides);
		await AuthorizationService.CheckAsync(SchoolIdentityPermissions.AccessSignature);
		var sourceYear = await access.EnsureAsync(input.SourceBoardId, input.SourceMediumId, manage: true, organizationId: input.SourceOrganizationId);
		var targetYear = await access.EnsureAsync(input.TargetBoardId, input.TargetMediumId, manage: true, organizationId: input.TargetOrganizationId);
		if (sourceYear == targetYear || input.SourceMediumId.HasValue != input.TargetMediumId.HasValue
			|| input.SourceOrganizationId.HasValue != input.TargetOrganizationId.HasValue)
			throw new BusinessException(SchoolIdentityErrorCodes.CopyContextInvalid);
		var source = await reader.FindAsync(input.SourceBoardId, input.SourceMediumId, input.SourceOrganizationId);
		if (source == null) throw new BusinessException(SchoolIdentityErrorCodes.SourceOverrideMissing);
		var result = await UpdateAsync(new()
		{
			BoardId = input.TargetBoardId, MediumId = input.TargetMediumId, OrganizationId = input.TargetOrganizationId,
			ConcurrencyStamp = input.TargetConcurrencyStamp, Choices = SchoolIdentityMapping.Dto(source.Choices, true)
		});
		var target = await reader.FindAsync(input.TargetBoardId, input.TargetMediumId, input.TargetOrganizationId);
		await revisions.InsertAsync(new(GuidGenerator.Create(), target!, $"Copied from profile {source.Id}"));
		return result;
	}

	public async Task<SchoolIdentityAssetDto> UploadAsync(SchoolIdentityAssetKind kind, Guid? boardId, Guid? mediumId,
		string fileName, byte[] bytes, Guid? organizationId = null)
	{
		await AuthorizationService.CheckAsync(SchoolIdentityPermissions.View);
		await AuthorizationService.CheckAsync(boardId.HasValue ? SchoolIdentityPermissions.ManageOverrides : SchoolIdentityPermissions.ManageDefaults);
		if (kind == SchoolIdentityAssetKind.Signature) await AuthorizationService.CheckAsync(SchoolIdentityPermissions.AccessSignature);
		await access.EnsureAsync(boardId, mediumId, manage: true, organizationId: organizationId);
		var (type, width, height) = SchoolIdentityImageValidation.Validate(kind, bytes);
		var id = GuidGenerator.Create();
		var name = Path.GetFileName(fileName);
		if (string.IsNullOrWhiteSpace(name) || name.Length > 256) throw new BusinessException(SchoolIdentityErrorCodes.FileNameInvalid);
		var blobName = $"school-identity/{CurrentTenant.Id:N}/{id:N}";
		await blobs.SaveAsync(blobName, bytes, overrideExisting: false);
		var asset = new SchoolIdentityAsset(id, CurrentTenant.Id!.Value, kind, blobName, name, type, bytes.LongLength, width, height, boardId, mediumId, organizationId);
		await assets.InsertAsync(asset, autoSave: true);
		return SchoolIdentityMapping.Dto(asset);
	}

	public async Task<SchoolIdentityAssetDownloadDto> DownloadAsync(Guid id, SchoolIdentityContextDto input)
	{
		await AuthorizationService.CheckAsync(SchoolIdentityPermissions.View);
		var year = await access.EnsureAsync(input.BoardId, input.MediumId, organizationId: input.OrganizationId);
		var asset = await assets.GetAsync(id);
		if (asset.TenantId != CurrentTenant.Id) throw new BusinessException(SchoolIdentityErrorCodes.AssetInvalid);
		var signature = asset.Kind == SchoolIdentityAssetKind.Signature;
		if (signature) await AuthorizationService.CheckAsync(SchoolIdentityPermissions.AccessSignature);
		var profile = await reader.ReadAsync(input.BoardId, input.MediumId, year, signature, organizationId: input.OrganizationId);
		var selectedId = profile.Choices.Signature?.AssetId;
		var referenced = profile.Effective.Values.Any(x => x.Selection.AssetId == id)
			|| profile.Choices.Logo.AssetId == id || profile.Choices.Banner.AssetId == id || selectedId == id;
		var ownDraft = asset.CreatorId.HasValue && asset.CreatorId == CurrentUser.Id && !asset.IsRetained
			&& asset.EducationBoardId == input.BoardId && asset.EducationMediumId == input.MediumId && asset.OrganizationId == input.OrganizationId;
		if (!referenced && !ownDraft)
		{
			if (!asset.IsRetained) throw new AbpAuthorizationException();
			await access.EnsureAsync(asset.EducationBoardId, asset.EducationMediumId, organizationId: asset.OrganizationId);
		}
		return new() { Content = await blobs.GetAllBytesAsync(asset.BlobName), ContentType = asset.ContentType, FileName = asset.FileName };
	}

	public async Task<SchoolBrandingDto> GetBrandingAsync()
	{
		if (!CurrentUser.IsAuthenticated) throw new AbpAuthorizationException();
		await access.EnsureAsync(null, null);
		var profile = await reader.ReadAsync(null, null, null, signature: false);
		return new() { Logo = BrandingAsset(profile.Effective["logo"]), Banner = BrandingAsset(profile.Effective["banner"]) };
	}

	private static SchoolBrandingAssetDto? BrandingAsset(SchoolIdentityEffectiveAssetDto effective)
	{
		var asset = effective.Selection.Mode == SchoolIdentityAssetMode.Custom ? effective.Asset : null;
		return asset == null ? null : new() { Id = asset.Id, ContentType = asset.ContentType, Width = asset.Width, Height = asset.Height };
	}

	public async Task<SchoolBrandingImageDto?> GetBrandingAssetAsync(SchoolIdentityAssetKind kind)
	{
		if (kind is not (SchoolIdentityAssetKind.Logo or SchoolIdentityAssetKind.Banner)) throw new AbpAuthorizationException();
		var branding = await GetBrandingAsync();
		var selected = kind == SchoolIdentityAssetKind.Logo ? branding.Logo : branding.Banner;
		if (selected == null) return null;
		var asset = await assets.GetAsync(selected.Id);
		if (asset.TenantId != CurrentTenant.Id || asset.Kind != kind) throw new AbpAuthorizationException();
		// Uploaded assets are immutable; replacing an image creates a new asset ID.
		return new() { Content = await blobs.GetAllBytesAsync(asset.BlobName), ContentType = asset.ContentType,
			EntityTag = $"\"{CurrentTenant.Id:N}-{asset.Id:N}\"" };
	}

	public async Task<SchoolIdentityAssetDownloadDto> PreviewAsync(SchoolIdentityPreviewDto input)
	{
		await AuthorizationService.CheckAsync(SchoolIdentityPermissions.View);
		var year = await access.EnsureAsync(input.BoardId, input.MediumId, organizationId: input.OrganizationId);
		var signature = await permissionChecker.IsGrantedAsync(SchoolIdentityPermissions.AccessSignature);
		SchoolIdentityChoices? draft = null;
		if (input.Choices != null)
		{
			await AuthorizationService.CheckAsync(input.BoardId.HasValue ? SchoolIdentityPermissions.ManageOverrides : SchoolIdentityPermissions.ManageDefaults);
			await access.EnsureAsync(input.BoardId, input.MediumId, manage: true, organizationId: input.OrganizationId);
			var original = (await reader.FindAsync(input.BoardId, input.MediumId, input.OrganizationId))?.Choices ?? new();
			if (input.Choices.Signature != null && !signature) throw new AbpAuthorizationException();
			draft = await PrepareChoicesAsync(input.Choices, original);
			await RetainAssetsAsync(draft, original, retain: false);
		}
		var profile = await reader.ReadAsync(input.BoardId, input.MediumId, year, signature, draft, input.OrganizationId);
		var snapshot = SchoolIdentityDocumentService.Snapshot(profile, CurrentTenant.Id!.Value);
		var images = await documents.LoadAsync(snapshot);
		return new() { Content = SchoolIdentityPreviewRenderer.Render(CurrentTenant.Name ?? "School", profile, images),
			ContentType = "application/pdf", FileName = "school-identity-preview.pdf" };
	}
}
