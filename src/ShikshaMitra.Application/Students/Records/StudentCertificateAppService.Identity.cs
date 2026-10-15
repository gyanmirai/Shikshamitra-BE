using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.TenantSettings.SchoolIdentity;
using Volo.Abp.Data;

namespace ShikshaMitra.Students.Records;

public partial class StudentCertificateAppService
{
	private const string IdentityChoicesProperty = "SchoolIdentityChoices";
	private async Task ConfigureIdentityAsync(StudentCertificateTemplate template, SchoolIdentityTemplateChoicesDto? input)
	{
		if (input == null) return;
		var original = ReadIdentityChoices(template);
		if (input.Logo != null) await identityTemplateAssets.ValidateAndRetainAsync(input.Logo, SchoolIdentityAssetKind.Logo);
		if (input.Banner != null) await identityTemplateAssets.ValidateAndRetainAsync(input.Banner, SchoolIdentityAssetKind.Banner);
		if (input.Signature != null)
		{
			await AuthorizationService.CheckAsync(SchoolIdentityPermissions.AccessSignature);
			await identityTemplateAssets.ValidateAndRetainAsync(input.Signature, SchoolIdentityAssetKind.Signature);
		}
		template.SetProperty(IdentityChoicesProperty, JsonSerializer.Serialize(input with {
			Logo = input.Logo ?? original?.Logo, Banner = input.Banner ?? original?.Banner,
			Signature = input.Signature ?? original?.Signature }, JsonOptions));
	}
	private static SchoolIdentityTemplateChoicesDto? ReadIdentityChoices(StudentCertificateTemplate template)
	{
		var json = template.GetProperty<string?>(IdentityChoicesProperty);
		return string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<SchoolIdentityTemplateChoicesDto>(json, JsonOptions);
	}
}
