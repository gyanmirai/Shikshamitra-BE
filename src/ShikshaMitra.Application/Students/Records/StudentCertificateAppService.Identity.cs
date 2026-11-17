using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.TenantSettings.SchoolIdentity;
using Volo.Abp.Data;

namespace ShikshaMitra.Students.Records;

public partial class StudentCertificateAppService
{
	private const string IdentityChoicesProperty = "SchoolIdentityChoices";

	private async Task PopulateBrandingAsync(StudentCertificateResponseDto response)
	{
		var snapshot = response.Template.SchoolIdentity;
		if (snapshot == null || (!snapshot.LogoAssetId.HasValue && !snapshot.BannerAssetId.HasValue)) return;
		// Load only the captured public letterhead assets; signatures remain in the existing PDF flow.
		var assets = await schoolIdentity.LoadAsync(new SchoolIdentityDocumentSnapshotDto
		{
			TenantId = snapshot.TenantId,
			LogoAssetId = snapshot.LogoAssetId,
			BannerAssetId = snapshot.BannerAssetId
		});
		response.Branding = new StudentCertificateBrandingDto
		{
			LogoDataUrl = BrandingDataUrl(assets.Logo),
			BannerDataUrl = BrandingDataUrl(assets.Banner)
		};
	}

	private static string? BrandingDataUrl(byte[]? bytes)
	{
		if (bytes == null) return null;
		var contentType = bytes.Length >= 2 && bytes[0] == 0xff && bytes[1] == 0xd8 ? "image/jpeg" : "image/png";
		return $"data:{contentType};base64,{Convert.ToBase64String(bytes)}";
	}

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
