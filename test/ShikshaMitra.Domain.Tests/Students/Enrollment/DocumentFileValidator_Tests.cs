using System;
using System.Security.Cryptography;
using ShikshaMitra.Students.Documents;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class DocumentFileValidator_Tests
{
	private static readonly byte[] Pdf = "%PDF-1.7 test"u8.ToArray();

	[Fact]
	public void Validated_Pdf_Has_Private_Storage_Metadata_And_Stable_Hash()
	{
		var result = Validate("birth.PDF", "APPLICATION/PDF", Pdf);
		result.SafeFileName.ShouldBe("birth.PDF");
		result.ContentType.ShouldBe("application/pdf");
		result.Sha256Hash.ShouldBe(Convert.ToHexString(SHA256.HashData(Pdf)).ToLowerInvariant());
		result.IsSensitive.ShouldBeFalse();
	}

	[Theory]
	[InlineData("Aadhaar card")]
	[InlineData("Caste certificate")]
	[InlineData("RTE certificate")]
	public void Sensitive_Document_Types_Remain_Classified(string label)
	{
		DocumentFileValidator.Validate("evidence.pdf", "application/pdf", Pdf.Length, Pdf,
			label, 1, [AcceptedFileFormat.Pdf]).IsSensitive.ShouldBeTrue();
	}

	[Theory]
	[InlineData("document", "application/pdf", "Document:InvalidFileName")]
	[InlineData("document.exe", "application/pdf", "Document:UnsupportedFileExtension")]
	[InlineData("document.jpg", "image/jpeg", "Document:InvalidFormat")]
	[InlineData("document.pdf", "image/png", "Document:ContentTypeMismatch")]
	public void Existing_Extension_And_Mime_Checks_Remain_Enforced(string name, string mime, string code)
	{
		Should.Throw<BusinessException>(() => Validate(name, mime, Pdf)).Code.ShouldBe(code);
	}

	[Fact]
	public void A_Renamed_Text_File_Does_Not_Pass_The_Pdf_Signature_Check()
	{
		Should.Throw<BusinessException>(() => Validate("evidence.pdf", "application/pdf", "not a PDF"u8.ToArray()))
			.Code.ShouldBe("Document:SignatureMismatch");
	}

	[Fact]
	public void Empty_And_Oversized_Files_Are_Rejected()
	{
		Should.Throw<BusinessException>(() => Validate("evidence.pdf", "application/pdf", []))
			.Code.ShouldBe("Document:EmptyFile");
		Should.Throw<BusinessException>(() => Validate("evidence.pdf", "application/pdf", new byte[1024 * 1024 + 1]))
			.Code.ShouldBe("Document:FileSizeExceeded");
	}

	[Fact]
	public void A_False_Reported_Size_Cannot_Bypass_The_Actual_Content_Check()
	{
		Should.Throw<BusinessException>(() => DocumentFileValidator.Validate(
			"evidence.pdf", "application/pdf", 1, Pdf, "Certificate", 1, [AcceptedFileFormat.Pdf]))
			.Code.ShouldBe("Document:SizeMismatch");
	}

	private static DocumentFileValidationResult Validate(string name, string mime, byte[] content) =>
		DocumentFileValidator.Validate(name, mime, content.Length, content, "Certificate", 1, [AcceptedFileFormat.Pdf]);
}
