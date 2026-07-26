using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Employees.Onboarding;

public class EmployeeApplicationUploadPolicy_Tests
{
	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("application/octet-stream")]
	public void Stored_Legacy_Resume_Metadata_Should_Derive_Canonical_Content_Type(
		string? storedContentType)
	{
		EmployeeApplicationUploadPolicy.TryNormalizeStored(
				"resume.pdf",
				storedContentType,
				EmployeeApplicationUploadKind.Resume,
				out var fileName,
				out var contentType)
			.ShouldBeTrue();
		fileName.ShouldBe("resume.pdf");
		contentType.ShouldBe("application/pdf");
	}

	[Fact]
	public void Stored_Legacy_Photo_Metadata_Should_Derive_Canonical_Jpeg_Content_Type()
	{
		EmployeeApplicationUploadPolicy.TryNormalizeStored(
				"photo.jpg",
				"application/octet-stream",
				EmployeeApplicationUploadKind.Photo,
				out var fileName,
				out var contentType)
			.ShouldBeTrue();
		fileName.ShouldBe("photo.jpg");
		contentType.ShouldBe("image/jpeg");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("application/octet-stream")]
	public void New_Uploads_Should_Still_Reject_Missing_Or_Generic_Content_Type(
		string? suppliedContentType)
	{
		EmployeeApplicationUploadPolicy.TryNormalize(
				"resume.pdf",
				suppliedContentType,
				EmployeeApplicationUploadKind.Resume,
				out _,
				out _)
			.ShouldBeFalse();
	}

	[Fact]
	public void Stored_Metadata_Should_Reject_Explicit_Content_Type_Mismatch()
	{
		EmployeeApplicationUploadPolicy.TryNormalizeStored(
				"resume.pdf",
				"text/html",
				EmployeeApplicationUploadKind.Resume,
				out _,
				out _)
			.ShouldBeFalse();
	}
}
