using System;
using Volo.Abp;

namespace ShikshaMitra.TenantSettings.SchoolIdentity;

public sealed record SchoolIdentitySelection
{
	public SchoolIdentityAssetMode Mode { get; init; }
	public Guid? AssetId { get; init; }
	public string? PrincipalName { get; init; }
	public string? Designation { get; init; }

	public SchoolIdentitySelection Validated(SchoolIdentityAssetKind kind)
	{
		if (!Enum.IsDefined(Mode) || (Mode == SchoolIdentityAssetMode.Custom && (!AssetId.HasValue || AssetId == Guid.Empty)))
			throw new BusinessException(SchoolIdentityErrorCodes.InvalidSelection);
		if (Mode != SchoolIdentityAssetMode.Custom)
		{
			if (AssetId.HasValue || PrincipalName != null || Designation != null)
				throw new BusinessException(SchoolIdentityErrorCodes.InvalidSelection);
			return this;
		}
		if (kind != SchoolIdentityAssetKind.Signature && (PrincipalName != null || Designation != null))
			throw new BusinessException(SchoolIdentityErrorCodes.InvalidSelection);
		if (kind == SchoolIdentityAssetKind.Signature)
			return this with
			{
				PrincipalName = Check.NotNullOrWhiteSpace(PrincipalName, nameof(PrincipalName), 128).Trim(),
				Designation = Check.NotNullOrWhiteSpace(Designation, nameof(Designation), 128).Trim()
			};
		return this;
	}
}

public sealed record SchoolIdentityChoices
{
	public SchoolIdentitySelection Logo { get; init; } = new();
	public SchoolIdentitySelection Banner { get; init; } = new();
	public SchoolIdentitySelection Signature { get; init; } = new();

	public SchoolIdentityChoices Validated() => new()
	{
		Logo = (Logo ?? throw new BusinessException(SchoolIdentityErrorCodes.InvalidSelection)).Validated(SchoolIdentityAssetKind.Logo),
		Banner = (Banner ?? throw new BusinessException(SchoolIdentityErrorCodes.InvalidSelection)).Validated(SchoolIdentityAssetKind.Banner),
		Signature = (Signature ?? throw new BusinessException(SchoolIdentityErrorCodes.InvalidSelection)).Validated(SchoolIdentityAssetKind.Signature)
	};

	public SchoolIdentitySelection Get(SchoolIdentityAssetKind kind) => kind switch
	{
		SchoolIdentityAssetKind.Logo => Logo,
		SchoolIdentityAssetKind.Banner => Banner,
		SchoolIdentityAssetKind.Signature => Signature,
		_ => throw new BusinessException(SchoolIdentityErrorCodes.InvalidAssetKind)
	};
}
