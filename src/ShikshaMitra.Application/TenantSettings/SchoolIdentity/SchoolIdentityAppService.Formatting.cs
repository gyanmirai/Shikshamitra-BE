using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.Formatting;

namespace ShikshaMitra.TenantSettings.SchoolIdentity;

public partial class SchoolIdentityAppService
{
	private async Task<SchoolIdentityChoices> PrepareChoicesAsync(SchoolIdentityChoicesDto input, SchoolIdentityChoices original)
	{
		var signature = input.Signature == null ? original.Signature : SchoolIdentityMapping.Domain(input.Signature);
		if (signature.Mode == SchoolIdentityAssetMode.Custom &&
			(signature.PrincipalName != original.Signature.PrincipalName || signature.Designation != original.Signature.Designation))
		{
			var rules = await LazyServiceProvider.LazyGetRequiredService<InstituteFieldFormattingStore>().GetAsync("schoolIdentity");
			signature = signature with
			{
				PrincipalName = signature.PrincipalName == original.Signature.PrincipalName ? signature.PrincipalName
					: InstituteFieldFormattingCatalog.Required(rules, "principalName", signature.PrincipalName!, 128),
				Designation = signature.Designation == original.Signature.Designation ? signature.Designation
					: InstituteFieldFormattingCatalog.Required(rules, "designation", signature.Designation!, 128)
			};
		}
		return new SchoolIdentityChoices
		{
			Logo = SchoolIdentityMapping.Domain(input.Logo),
			Banner = SchoolIdentityMapping.Domain(input.Banner),
			Signature = signature
		}.Validated();
	}
}
