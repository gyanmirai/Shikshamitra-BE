namespace ShikshaMitra.TenantSettings.SchoolIdentity;

internal static class SchoolIdentityMapping
{
	internal static SchoolIdentitySelectionDto Dto(SchoolIdentitySelection value) => new()
	{
		Mode = value.Mode, AssetId = value.AssetId, PrincipalName = value.PrincipalName, Designation = value.Designation
	};
	internal static SchoolIdentitySelection Domain(SchoolIdentitySelectionDto value) => value == null
		? throw new Volo.Abp.BusinessException(SchoolIdentityErrorCodes.InvalidSelection) : new()
	{
		Mode = value.Mode, AssetId = value.AssetId, PrincipalName = value.PrincipalName, Designation = value.Designation
	};
	internal static SchoolIdentityChoicesDto Dto(SchoolIdentityChoices value, bool signature) => new()
	{
		Logo = Dto(value.Logo), Banner = Dto(value.Banner), Signature = signature ? Dto(value.Signature) : null
	};
	internal static SchoolIdentityAssetDto Dto(SchoolIdentityAsset value) => new()
	{
		Id = value.Id, Kind = value.Kind, FileName = value.FileName, ContentType = value.ContentType,
		Size = value.Size, Width = value.Width, Height = value.Height
	};
}
