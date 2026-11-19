using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.AcademicSetups.Formatting;
using ShikshaMitra.TenantSettings.Formatting;
using ShikshaMitra.TenantSettings.SchoolIdentity;
using Shouldly;
using Xunit;

namespace ShikshaMitra.TenantSettings;

public partial class SchoolIdentityIntegration_Tests
{
	[Fact]
	public Task Signature_rules_format_changed_text_and_preserve_unchanged_signature_details() => RunAsync(async (_, _) =>
	{
		var store = GetRequiredService<InstituteFieldFormattingStore>();
		var rules = InstituteFieldFormattingCatalog.Defaults("schoolIdentity");
		rules["principalName"] = AcademicTextCase.Capitalize;
		rules["designation"] = AcademicTextCase.CamelCase;
		await store.SetAsync("schoolIdentity", rules);
		var asset = await Service.UploadAsync(SchoolIdentityAssetKind.Signature, null, null, "signature.png", Png);
		var saved = await Service.UpdateAsync(new() { Choices = new() { Signature = new() {
			Mode = SchoolIdentityAssetMode.Custom, AssetId = asset.Id, PrincipalName = "sCHOOL pRINCIPAL", Designation = "head teacher" } } });
		saved.Choices.Signature!.PrincipalName.ShouldBe("School Principal");
		saved.Choices.Signature.Designation.ShouldBe("headTeacher");
		rules["principalName"] = AcademicTextCase.Uppercase;
		rules["designation"] = AcademicTextCase.Uppercase;
		await store.SetAsync("schoolIdentity", rules);
		var unchanged = await Service.UpdateAsync(new() { ConcurrencyStamp = saved.ConcurrencyStamp, Choices = saved.Choices });
		unchanged.Choices.Signature!.PrincipalName.ShouldBe("School Principal");
		unchanged.Choices.Signature.Designation.ShouldBe("headTeacher");
		var changed = await Service.UpdateAsync(new() { ConcurrencyStamp = unchanged.ConcurrencyStamp, Choices = unchanged.Choices with {
			Signature = unchanged.Choices.Signature with { PrincipalName = "new principal" } } });
		changed.Choices.Signature!.PrincipalName.ShouldBe("NEW PRINCIPAL");
		changed.Choices.Signature.Designation.ShouldBe("headTeacher");
	});
}
