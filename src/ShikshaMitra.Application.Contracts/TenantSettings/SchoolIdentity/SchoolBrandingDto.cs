using System;

namespace ShikshaMitra.TenantSettings.SchoolIdentity;

public sealed class SchoolBrandingDto
{
	public SchoolBrandingAssetDto? Logo { get; set; }
	public SchoolBrandingAssetDto? Banner { get; set; }
}

public sealed class SchoolBrandingAssetDto
{
	public Guid Id { get; set; }
	public string ContentType { get; set; } = "";
	public int Width { get; set; }
	public int Height { get; set; }
}

public sealed class SchoolBrandingImageDto
{
	public byte[] Content { get; set; } = [];
	public string ContentType { get; set; } = "";
	public string EntityTag { get; set; } = "";
}
