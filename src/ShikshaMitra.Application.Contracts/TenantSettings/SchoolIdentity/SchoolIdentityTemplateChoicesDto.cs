namespace ShikshaMitra.TenantSettings.SchoolIdentity;

public sealed record SchoolIdentityTemplateChoicesDto
{
	public SchoolIdentitySelectionDto? Logo { get; init; }
	public SchoolIdentitySelectionDto? Banner { get; init; }
	public SchoolIdentitySelectionDto? Signature { get; init; }
}
