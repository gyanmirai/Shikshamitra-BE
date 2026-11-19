using System.Threading.Tasks;
using ShikshaMitra.HR.Employees.ICards;
using ShikshaMitra.TenantSettings.SchoolIdentity;
using Shouldly;
using Xunit;

namespace ShikshaMitra.TenantSettings;

public partial class SchoolIdentityIntegration_Tests
{
	[Fact]
	public Task Staff_shared_logo_preview_uses_current_school_identity_and_honours_omission() => RunAsync(async (_, _) =>
	{
		var logo = await Service.UploadAsync(SchoolIdentityAssetKind.Logo, null, null, "staff-shared-logo.png", Png);
		var saved = await Service.UpdateAsync(new() { Choices = new() { Logo = Custom(logo.Id) } });
		var service = GetRequiredService<EmployeeICardSchoolLogoService>();
		var preview = await service.GetAsync(null);
		preview!.Content.ShouldBe(Png); preview.ContentType.ShouldBe("image/png");
		await Service.UpdateAsync(new() { ConcurrencyStamp = saved.ConcurrencyStamp,
			Choices = new() { Logo = new() { Mode = SchoolIdentityAssetMode.Hidden } } });
		(await service.GetAsync(null)).ShouldBeNull();
	});
}
