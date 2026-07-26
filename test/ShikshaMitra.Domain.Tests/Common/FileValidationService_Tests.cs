using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Common;

public class FileValidationService_Tests
{
	[Fact]
	public void ValidateDocumentUpload_Should_Reject_Html_Disguised_As_Pdf()
	{
		var content = Encoding.UTF8.GetBytes("<html><script>alert(1)</script></html>");

		Should.Throw<BusinessException>(() =>
			FileValidationService.ValidateDocumentUpload("resume.pdf", content, content.Length));
	}

	[Fact]
	public void ValidateDocumentUpload_Should_Reject_Arbitrary_Zip_Disguised_As_Docx()
	{
		var content = BuildZip(("payload.html", "<script>alert(1)</script>"));

		Should.Throw<BusinessException>(() =>
			FileValidationService.ValidateDocumentUpload("resume.docx", content, content.Length));
	}

	[Fact]
	public void ValidateDocumentUpload_Should_Reject_Malformed_Word_OpenXml_Package()
	{
		var content = BuildZip(
			("[Content_Types].xml", WordContentTypes),
			("_rels/.rels", WordRootRelationships),
			("word/document.xml", "<w:document />"));

		Should.Throw<BusinessException>(() =>
			FileValidationService.ValidateDocumentUpload("resume.docx", content, content.Length));
	}

	[Fact]
	public void ValidateDocumentUpload_Should_Accept_Valid_Word_OpenXml_Package()
	{
		var content = BuildZip(
			("[Content_Types].xml", WordContentTypes),
			("_rels/.rels", WordRootRelationships),
			("word/document.xml", WordDocument));

		Should.NotThrow(() =>
			FileValidationService.ValidateDocumentUpload("resume.docx", content, content.Length));
	}

	[Fact]
	public void ValidateDocumentUpload_Should_Accept_Valid_Excel_OpenXml_Package()
	{
		var content = BuildZip(
			("[Content_Types].xml", ExcelContentTypes),
			("_rels/.rels", ExcelRootRelationships),
			("xl/workbook.xml", ExcelWorkbook));

		Should.NotThrow(() =>
			FileValidationService.ValidateDocumentUpload("records.xlsx", content, content.Length));
	}

	[Fact]
	public void ValidateDocumentUpload_Should_Reject_OpenXml_Package_With_Too_Many_Entries()
	{
		var entries = Enumerable.Range(0, 513)
			.Select(index => ($"customXml/item{index}.xml", "<item />"))
			.ToArray();
		var content = BuildZip(entries);

		Should.Throw<BusinessException>(() =>
			FileValidationService.ValidateDocumentUpload("resume.docx", content, content.Length));
	}

	[Fact]
	public void ValidateDocumentUpload_Should_Reject_Macro_Content_In_Docx()
	{
		var content = BuildZip(
			("[Content_Types].xml", WordContentTypes),
			("_rels/.rels", WordRootRelationships),
			("word/document.xml", WordDocument),
			("word/vbaProject.bin", "macro"));

		Should.Throw<BusinessException>(() =>
			FileValidationService.ValidateDocumentUpload("resume.docx", content, content.Length));
	}

	[Fact]
	public void ValidateProfilePictureUpload_Should_Reject_Svg()
	{
		var content = Encoding.UTF8.GetBytes("<svg onload=\"alert(1)\"></svg>");

		Should.Throw<BusinessException>(() =>
			FileValidationService.ValidateProfilePictureUpload("photo.svg", content));
	}

	private static byte[] BuildZip(params (string Name, string Content)[] entries)
	{
		using var stream = new MemoryStream();
		using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
		{
			foreach (var (name, content) in entries)
			{
				var entry = archive.CreateEntry(name);
				using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
				writer.Write(content);
			}
		}

		return stream.ToArray();
	}

	private const string WordContentTypes = """
		<?xml version="1.0" encoding="UTF-8"?>
		<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
		  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" />
		  <Default Extension="xml" ContentType="application/xml" />
		  <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml" />
		</Types>
		""";

	private const string WordRootRelationships = """
		<?xml version="1.0" encoding="UTF-8"?>
		<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
		  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml" />
		</Relationships>
		""";

	private const string WordDocument = """
		<?xml version="1.0" encoding="UTF-8"?>
		<w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
		  <w:body />
		</w:document>
		""";

	private const string ExcelContentTypes = """
		<?xml version="1.0" encoding="UTF-8"?>
		<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
		  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" />
		  <Default Extension="xml" ContentType="application/xml" />
		  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml" />
		</Types>
		""";

	private const string ExcelRootRelationships = """
		<?xml version="1.0" encoding="UTF-8"?>
		<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
		  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml" />
		</Relationships>
		""";

	private const string ExcelWorkbook = """
		<?xml version="1.0" encoding="UTF-8"?>
		<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
		  <sheets />
		</workbook>
		""";
}
