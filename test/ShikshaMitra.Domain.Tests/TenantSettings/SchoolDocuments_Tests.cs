using System;
using ShikshaMitra.TenantSettings.SchoolDocuments;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Xunit;

namespace ShikshaMitra.TenantSettings;

public class SchoolDocuments_Tests
{
	[Theory]
	[InlineData("approval.pdf", "application/pdf", "%PDF-1.7\n")]
	[InlineData("scan.jpg", "image/jpeg", "jpeg")]
	[InlineData("scan.jpeg", "image/jpeg", "jpeg")]
	[InlineData("scan.png", "image/png", "png")]
	public void Upload_requires_matching_extension_content_type_and_signature(string name, string type, string value)
	{
		var bytes = Bytes(value);
		SchoolDocumentFileRules.Validate(name, type, bytes).ContentType.ShouldBe(type);
		Should.Throw<BusinessException>(() => SchoolDocumentFileRules.Validate(name, "text/html", bytes));
		Should.Throw<BusinessException>(() => SchoolDocumentFileRules.Validate(name, type, "<html>fake</html>"u8.ToArray()));
		Should.Throw<BusinessException>(() => SchoolDocumentFileRules.Validate(name + ".exe", type, bytes));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(6)]
	public void Document_requires_between_one_and_five_attachments(int count) =>
		Should.Throw<BusinessException>(() => SchoolDocumentFileRules.ValidateCount(count));

	[Fact]
	public void File_size_boundaries_and_safe_name_are_enforced()
	{
		Should.Throw<BusinessException>(() => SchoolDocumentFileRules.Validate("empty.pdf", "application/pdf", []));
		var maximum = new byte[SchoolDocumentConsts.MaxFileBytes];
		"%PDF-"u8.CopyTo(maximum);
		SchoolDocumentFileRules.Validate("../../approval.pdf", "application/pdf", maximum).FileName.ShouldBe("approval.pdf");
		Should.Throw<BusinessException>(() => SchoolDocumentFileRules.Validate("large.pdf", "application/pdf", new byte[maximum.Length + 1]));
	}

	[Fact]
	public void Details_and_concurrency_are_validated_without_changing_saved_values()
	{
		var document = new SchoolDocument(Guid.NewGuid(), Guid.NewGuid(), " Approval ", SchoolDocumentCategory.GovernmentApproval, " Notes ", "staff");
		document.Name.ShouldBe("Approval");
		document.Notes.ShouldBe("Notes");
		Should.Throw<AbpDbConcurrencyException>(() => document.UpdateDetails("Changed", document.Category, null, "stale"));
		document.Name.ShouldBe("Approval");
		Should.Throw<BusinessException>(() => document.UpdateDetails(" ", document.Category, null, document.ConcurrencyStamp));
		Should.Throw<BusinessException>(() => document.UpdateDetails("Name", (SchoolDocumentCategory)99, null, document.ConcurrencyStamp));
		Should.Throw<BusinessException>(() => document.UpdateDetails("Name", document.Category, new string('x', 2001), document.ConcurrencyStamp));
	}

	private static byte[] Bytes(string value) => value switch
	{
		"png" => [137, 80, 78, 71, 13, 10, 26, 10, 0],
		"jpeg" => [255, 216, 255, 0],
		_ => System.Text.Encoding.ASCII.GetBytes(value)
	};
}
