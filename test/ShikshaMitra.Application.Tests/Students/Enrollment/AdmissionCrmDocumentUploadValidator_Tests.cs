using System;
using System.Text;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.Enrollment;

public class AdmissionCrmDocumentUploadValidator_Tests
{
	private static readonly DateTime Today = new(2026, 8, 7, 12, 0, 0, DateTimeKind.Utc);

	[Fact]
	public void Valid_pdf_signature_and_mime_type_are_accepted()
	{
		var upload = CreateUpload("birth-certificate.pdf", "application/pdf", "%PDF-1.4\nvalid");

		Should.NotThrow(() => AdmissionCrmDocumentUploadValidator.Validate(CreateRequirement(), upload, Today));
	}

	[Fact]
	public void Spoofed_pdf_extension_is_rejected_by_magic_bytes()
	{
		var upload = CreateUpload("birth-certificate.pdf", "application/pdf", "not-a-pdf");

		var exception = Should.Throw<BusinessException>(() =>
			AdmissionCrmDocumentUploadValidator.Validate(CreateRequirement(), upload, Today));

		exception.Code.ShouldBe("AdmissionCrm:DocumentContentMismatch");
	}

	[Fact]
	public void Short_legacy_office_content_with_a_wrong_signature_is_rejected()
	{
		var requirement = CreateRequirement();
		requirement.AcceptedExtensions = ["doc"];
		var upload = CreateUpload("report.doc", "application/msword", "junk");

		var exception = Should.Throw<BusinessException>(() =>
			AdmissionCrmDocumentUploadValidator.Validate(requirement, upload, Today));

		exception.Code.ShouldBe("AdmissionCrm:DocumentContentMismatch");
	}

	[Fact]
	public void Pdf_bytes_with_a_mismatched_declared_mime_type_are_rejected()
	{
		var upload = CreateUpload("birth-certificate.pdf", "text/plain", "%PDF-1.4\nvalid");

		var exception = Should.Throw<BusinessException>(() =>
			AdmissionCrmDocumentUploadValidator.Validate(CreateRequirement(), upload, Today));

		exception.Code.ShouldBe("AdmissionCrm:DocumentContentTypeMismatch");
	}

	[Theory]
	[InlineData("folder\\birth-certificate.pdf")]
	[InlineData("folder/birth-certificate.pdf")]
	public void Path_bearing_file_name_is_rejected_before_blob_name_creation(string fileName)
	{
		var upload = CreateUpload(fileName, "application/pdf", "%PDF-1.4\nvalid");

		var exception = Should.Throw<BusinessException>(() =>
			AdmissionCrmDocumentUploadValidator.Validate(CreateRequirement(), upload, Today));

		exception.Code.ShouldBe("AdmissionCrm:InvalidDocumentFileName");
	}

	[Fact]
	public void Oversized_file_name_is_rejected_before_blob_name_creation()
	{
		var fileName = new string('a', AdmissionCrmConsts.MaxDocumentFileNameLength) + ".pdf";
		var upload = CreateUpload(fileName, "application/pdf", "%PDF-1.4\nvalid");

		var exception = Should.Throw<BusinessException>(() =>
			AdmissionCrmDocumentUploadValidator.Validate(CreateRequirement(), upload, Today));

		exception.Code.ShouldBe("AdmissionCrm:InvalidDocumentFileName");
	}

	[Fact]
	public void Requirement_cannot_make_an_unsafe_executable_extension_uploadable()
	{
		var requirement = CreateRequirement();
		requirement.AcceptedExtensions = ["exe"];
		var upload = CreateUpload("document.exe", "application/octet-stream", "MZ executable");

		var exception = Should.Throw<BusinessException>(() =>
			AdmissionCrmDocumentUploadValidator.Validate(requirement, upload, Today));

		exception.Code.ShouldBe("AdmissionCrm:DocumentFileTypeNotAccepted");
	}

	[Fact]
	public void Declared_size_must_match_content_and_respect_the_requirement_limit()
	{
		var mismatch = CreateUpload("birth-certificate.pdf", "application/pdf", "%PDF-1.4\nvalid");
		mismatch.Size++;
		var oversizedRequirement = CreateRequirement();
		oversizedRequirement.MaximumSizeMb = 0.00001m;
		var oversized = CreateUpload("birth-certificate.pdf", "application/pdf", "%PDF-1.4\nvalid");

		Should.Throw<BusinessException>(() =>
			AdmissionCrmDocumentUploadValidator.Validate(CreateRequirement(), mismatch, Today))
			.Code.ShouldBe("AdmissionCrm:InvalidDocumentContent");
		Should.Throw<BusinessException>(() =>
			AdmissionCrmDocumentUploadValidator.Validate(oversizedRequirement, oversized, Today))
			.Code.ShouldBe("AdmissionCrm:DocumentFileSizeExceeded");
	}

	private static AdmissionCrmDocumentRequirementDto CreateRequirement() => new()
	{
		Key = "birth-certificate",
		Name = "Birth certificate",
		RequirementLevel = AdmissionCrmDocumentRequirementLevel.Required,
		MaximumSizeMb = 2,
		AcceptedExtensions = ["pdf"]
	};

	private static ProcessedAdmissionCrmApplicantDocumentUploadDto CreateUpload(
		string fileName,
		string contentType,
		string content)
	{
		var bytes = Encoding.UTF8.GetBytes(content);
		return new ProcessedAdmissionCrmApplicantDocumentUploadDto
		{
			RequirementKey = "birth-certificate",
			FileName = fileName,
			ContentType = contentType,
			Size = bytes.LongLength,
			Content = bytes
		};
	}
}
