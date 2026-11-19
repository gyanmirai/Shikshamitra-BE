using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.TenantSettings.SchoolIdentity;

[RemoteService(false)]
public class SchoolIdentityTemplateAssetService(IRepository<SchoolIdentityAsset, Guid> assets,
	SchoolIdentityScopeAccess access) : ApplicationService, ITransientDependency
{
	public async Task ValidateAndRetainAsync(SchoolIdentitySelectionDto selection, SchoolIdentityAssetKind kind)
	{
		var value = SchoolIdentityMapping.Domain(selection).Validated(kind);
		if (value.Mode != SchoolIdentityAssetMode.Custom) return;
		await AuthorizationService.CheckAsync(SchoolIdentityPermissions.View);
		if (kind == SchoolIdentityAssetKind.Signature) await AuthorizationService.CheckAsync(SchoolIdentityPermissions.AccessSignature);
		var asset = await assets.GetAsync(value.AssetId!.Value);
		if (asset.TenantId != CurrentTenant.Id || asset.Kind != kind) throw new BusinessException(SchoolIdentityErrorCodes.AssetInvalid);
		if (!asset.IsRetained && asset.CreatorId != CurrentUser.Id) throw new AbpAuthorizationException();
		await access.EnsureAsync(asset.EducationBoardId, asset.EducationMediumId, organizationId: asset.OrganizationId);
		asset.Retain(); await assets.UpdateAsync(asset, autoSave: true);
	}

	public static void Apply(SchoolIdentityDocumentSnapshotDto snapshot, SchoolIdentitySelectionDto selection, SchoolIdentityAssetKind kind)
	{
		if (selection.Mode == SchoolIdentityAssetMode.Inherit) return;
		var id = selection.Mode == SchoolIdentityAssetMode.Custom ? selection.AssetId : null;
		switch (kind)
		{
			case SchoolIdentityAssetKind.Logo: snapshot.LogoAssetId = id; break;
			case SchoolIdentityAssetKind.Banner: snapshot.BannerAssetId = id; break;
			case SchoolIdentityAssetKind.Signature:
				snapshot.SignatureAssetId = id;
				snapshot.PrincipalName = id.HasValue ? selection.PrincipalName : null;
				snapshot.Designation = id.HasValue ? selection.Designation : null;
				break;
			default: throw new BusinessException(SchoolIdentityErrorCodes.InvalidAssetKind);
		}
	}
}
