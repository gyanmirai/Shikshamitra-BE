using System;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentRecords;
using ShikshaMitra.TenantSettings.SchoolIdentity;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Certificate_branding_is_opt_in_and_issued_and_duplicate_reads_keep_the_saved_images() => RunCertificationScenarioAsync(async () =>
	{
		using var identityActor = _principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity(
			BuildAdminPrincipal().Claims.Append(new Claim(AbpClaimTypes.TenantId, TestTenantId.ToString())), "Test")));
		var offering = await CreateCertificateStudentOfferingAsync();
		var student = await CreateExistingStudentAsync(offering, "CertificateBranding", "Student");
		await GrantCompletedRecordAsync(student);
		var identity = GetRequiredService<ISchoolIdentityAppService>();
		var logoBytes = SchoolIdentityTestImage.Png(32, 32);
		var bannerBytes = SchoolIdentityTestImage.Png(1400, 300);
		var signatureBytes = SchoolIdentityTestImage.Png(48, 48);
		var logo = await identity.UploadAsync(SchoolIdentityAssetKind.Logo, null, null, "certificate-logo.png", logoBytes);
		var banner = await identity.UploadAsync(SchoolIdentityAssetKind.Banner, null, null, "certificate-banner.png", bannerBytes);
		var signature = await identity.UploadAsync(SchoolIdentityAssetKind.Signature, null, null, "certificate-signature.png", signatureBytes);
		var saved = await identity.UpdateAsync(new() { Choices = new()
		{
			Logo = new() { Mode = SchoolIdentityAssetMode.Custom, AssetId = logo.Id },
			Banner = new() { Mode = SchoolIdentityAssetMode.Custom, AssetId = banner.Id },
			Signature = new() { Mode = SchoolIdentityAssetMode.Custom, AssetId = signature.Id, PrincipalName = "Test Principal", Designation = "Principal" }
		} });
		var service = GetRequiredService<IStudentCertificateAppService>();
		var request = new CreateStudentCertificateDto { StudentId = student.Id, CertificateType = StudentCertificateType.Bonafide, IdempotencyKey = $"branding-{student.Id:N}" };
		(await service.PreviewAsync(request)).Branding.ShouldBeNull();
		request.IncludeBranding = true;
		var preview = await service.PreviewAsync(request);
		preview.Branding.ShouldNotBeNull();
		preview.Branding.LogoDataUrl.ShouldBe($"data:image/png;base64,{Convert.ToBase64String(logoBytes)}");
		preview.Branding.BannerDataUrl.ShouldBe($"data:image/png;base64,{Convert.ToBase64String(bannerBytes)}");
		JsonSerializer.Serialize(preview.Branding).ShouldNotContain(Convert.ToBase64String(signatureBytes));
		var issued = await service.GenerateAsync(request);
		issued.Branding.ShouldBeNull();
		(await service.GetAsync(issued.CertificateId!.Value)).Branding.ShouldBeNull();

		var omitRequest = new CreateStudentCertificateDto
		{
			StudentId = student.Id, CertificateType = StudentCertificateType.Bonafide,
			IdempotencyKey = $"branding-omitted-{student.Id:N}", IncludeBranding = true, OmitSchoolBranding = true
		};
		var omittedPreview = await service.PreviewAsync(omitRequest);
		omittedPreview.Branding.ShouldBeNull();
		omittedPreview.Template.SchoolIdentity!.LogoAssetId.ShouldBeNull();
		omittedPreview.Template.SchoolIdentity.BannerAssetId.ShouldBeNull();
		omittedPreview.Template.SchoolIdentity.SignatureAssetId.ShouldBe(signature.Id);
		omittedPreview.Organization.OrganizationName.ShouldBe(preview.Organization.OrganizationName);
		omittedPreview.Organization.Address.ShouldBe(preview.Organization.Address);
		var omitted = await service.GenerateAsync(omitRequest);
		var omittedId = omitted.CertificateId.ShouldNotBeNull();
		(await service.GenerateAsync(omitRequest)).CertificateId.ShouldBe(omittedId);
		omitRequest.OmitSchoolBranding = false;
		(await Should.ThrowAsync<BusinessException>(() => service.GenerateAsync(omitRequest)))
			.Code.ShouldBe("Students:Records:DocumentIdempotencyConflict");

		var hidden = await identity.UpdateAsync(new() { ConcurrencyStamp = saved.ConcurrencyStamp, Choices = new()
		{
			Logo = new() { Mode = SchoolIdentityAssetMode.Hidden }, Banner = new() { Mode = SchoolIdentityAssetMode.Hidden },
			Signature = new() { Mode = SchoolIdentityAssetMode.Hidden }
		} });
		(await service.GetAsync(issued.CertificateId.Value, true)).Branding!.LogoDataUrl.ShouldBe(preview.Branding.LogoDataUrl);
		(await service.GetAsync(issued.CertificateId.Value, true)).Branding!.BannerDataUrl.ShouldBe(preview.Branding.BannerDataUrl);
		(await service.PreviewAsync(request)).Branding.ShouldBeNull();
		var duplicate = await service.DuplicateAsync(issued.CertificateId.Value, new() { Reason = "Additional saved snapshot copy", IdempotencyKey = $"branding-duplicate-{student.Id:N}" });
		(await service.GetAsync(duplicate.CertificateId!.Value, true)).Branding!.BannerDataUrl.ShouldBe(preview.Branding.BannerDataUrl);
		await identity.UpdateAsync(new() { ConcurrencyStamp = hidden.ConcurrencyStamp, Choices = new()
		{
			Logo = new() { Mode = SchoolIdentityAssetMode.Custom, AssetId = logo.Id },
			Banner = new() { Mode = SchoolIdentityAssetMode.Custom, AssetId = banner.Id }
		} });
		var omittedCopy = await service.DuplicateAsync(omittedId, new() { Reason = "Copy with saved branding choice" });
		var omittedCopyId = omittedCopy.CertificateId.ShouldNotBeNull();
		foreach (var certificateId in new[] { omittedId, omittedCopyId })
		{
			var stored = await service.GetAsync(certificateId, includeBranding: true);
			stored.Branding.ShouldBeNull();
			stored.Template.SchoolIdentity!.LogoAssetId.ShouldBeNull();
			stored.Template.SchoolIdentity.BannerAssetId.ShouldBeNull();
			stored.Template.SchoolIdentity.SignatureAssetId.ShouldBe(signature.Id);
			AssertOmittedPdfBranding(await service.GeneratePdfAsync(certificateId));
		}
		AssertOmittedPdfBranding(await service.ReprintAsync(omittedId, new() { Reason = "Verify saved branding on reprint" }));
		var brandedPdf = await service.GeneratePdfAsync(issued.CertificateId.Value);
		Encoding.ASCII.GetString(brandedPdf.Content).ShouldContain("/Width 1400");
		using (_currentTenant.Change(Guid.NewGuid())) await Should.ThrowAsync<Exception>(() => service.GetAsync(issued.CertificateId.Value, true));
	});

	private static void AssertOmittedPdfBranding(StudentCertificatePdfDto pdf)
	{
		var content = Encoding.ASCII.GetString(pdf.Content);
		content.ShouldStartWith("%PDF");
		// The known fixture widths distinguish banner/logo images from the retained signature.
		content.ShouldNotContain("/Width 1400");
		content.ShouldNotContain("/Width 32");
		content.ShouldContain("/Width 48");
	}
}
