using System;
using System.Collections.Generic;

namespace ShikshaMitra.TenantSettings.SchoolIdentity;

public sealed record SchoolIdentitySelectionDto
{
	public SchoolIdentityAssetMode Mode { get; init; }
	public Guid? AssetId { get; init; }
	public string? PrincipalName { get; init; }
	public string? Designation { get; init; }
}

public sealed record SchoolIdentityChoicesDto
{
	public SchoolIdentitySelectionDto Logo { get; init; } = new();
	public SchoolIdentitySelectionDto Banner { get; init; } = new();
	public SchoolIdentitySelectionDto? Signature { get; init; }
}

public sealed class SchoolIdentityContextDto
{
	public Guid? OrganizationId { get; set; }
	public Guid? BoardId { get; set; }
	public Guid? MediumId { get; set; }
}

public sealed class SchoolIdentityUpdateDto
{
	public Guid? OrganizationId { get; set; }
	public Guid? BoardId { get; set; }
	public Guid? MediumId { get; set; }
	public string ConcurrencyStamp { get; set; } = "";
	public SchoolIdentityChoicesDto Choices { get; set; } = new();
}

public sealed class SchoolIdentityProfileDto
{
	public Guid? OrganizationId { get; set; }
	public Guid? Id { get; set; }
	public Guid? AcademicYearId { get; set; }
	public Guid? BoardId { get; set; }
	public Guid? MediumId { get; set; }
	public string ConcurrencyStamp { get; set; } = "";
	public SchoolIdentityChoicesDto Choices { get; set; } = new();
	public Dictionary<string, SchoolIdentityEffectiveAssetDto> Effective { get; set; } = new();
	public bool CanAccessSignature { get; set; }
	public bool CanManage { get; set; }
}

public sealed class SchoolIdentityEffectiveAssetDto
{
	public SchoolIdentitySelectionDto Selection { get; set; } = new();
	public string Source { get; set; } = "";
	public SchoolIdentityAssetDto? Asset { get; set; }
}

public sealed class SchoolIdentityAssetDto
{
	public Guid Id { get; set; }
	public SchoolIdentityAssetKind Kind { get; set; }
	public string FileName { get; set; } = "";
	public string ContentType { get; set; } = "";
	public long Size { get; set; }
	public int Width { get; set; }
	public int Height { get; set; }
}

public sealed class SchoolIdentityAssetDownloadDto
{
	public byte[] Content { get; set; } = [];
	public string ContentType { get; set; } = "";
	public string FileName { get; set; } = "";
}

public sealed class SchoolIdentityRevisionDto
{
	public Guid Id { get; set; }
	public DateTime CreationTime { get; set; }
	public Guid? CreatorId { get; set; }
	public string? CreatorName { get; set; }
	public string Reason { get; set; } = "";
	public SchoolIdentityChoicesDto Choices { get; set; } = new();
}

public sealed class SchoolIdentityCopyDto
{
	public Guid? SourceOrganizationId { get; set; }
	public Guid? TargetOrganizationId { get; set; }
	public Guid SourceBoardId { get; set; }
	public Guid? SourceMediumId { get; set; }
	public Guid TargetBoardId { get; set; }
	public Guid? TargetMediumId { get; set; }
	public string TargetConcurrencyStamp { get; set; } = "";
}
