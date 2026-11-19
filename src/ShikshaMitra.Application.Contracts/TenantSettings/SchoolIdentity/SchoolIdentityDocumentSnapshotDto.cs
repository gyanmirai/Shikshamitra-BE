using System;

namespace ShikshaMitra.TenantSettings.SchoolIdentity;

public sealed class SchoolIdentityDocumentSnapshotDto
{
	public Guid? OrganizationId { get; set; }
	public Guid TenantId { get; set; }
	public Guid? BoardId { get; set; }
	public Guid? MediumId { get; set; }
	public Guid? LogoAssetId { get; set; }
	public Guid? BannerAssetId { get; set; }
	public Guid? SignatureAssetId { get; set; }
	public string? PrincipalName { get; set; }
	public string? Designation { get; set; }
}

public sealed class SchoolIdentityPreviewDto
{
	public Guid? OrganizationId { get; set; }
	public Guid? BoardId { get; set; }
	public Guid? MediumId { get; set; }
	public SchoolIdentityChoicesDto? Choices { get; set; }
}
