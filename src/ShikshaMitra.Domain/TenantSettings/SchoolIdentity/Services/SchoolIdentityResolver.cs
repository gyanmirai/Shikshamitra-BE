using System.Collections.Generic;

namespace ShikshaMitra.TenantSettings.SchoolIdentity;

public sealed record ResolvedSchoolIdentityAsset(SchoolIdentitySelection Selection, string Source);

public static class SchoolIdentityResolver
{
	public static ResolvedSchoolIdentityAsset Resolve(SchoolIdentityAssetKind kind,
		SchoolIdentityChoices? defaults, SchoolIdentityChoices? board, SchoolIdentityChoices? medium, SchoolIdentityChoices? organization = null)
	{
		foreach (var (choices, source) in new List<(SchoolIdentityChoices?, string)>
		{
			(organization, "Organization override"), (medium, "Medium override"), (board, "Board override"), (defaults, "School default")
		})
		{
			var selection = choices?.Get(kind);
			if (selection != null && selection.Mode != SchoolIdentityAssetMode.Inherit)
				return new(selection, source);
		}
		return new(new(), "Not configured");
	}
}
