using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Volo.Abp;

namespace ShikshaMitra.Common;

/// <summary>
/// Static utility for validating file uploads — size, extension whitelist, and magic-byte checks.
/// Extracted as an infrastructure concern so it can be reused across domain services.
/// </summary>
public static class FileValidationService
{
	private const int MaxOpenXmlEntryCount = 512;
	private const long MaxOpenXmlUncompressedBytes = 50L * 1024 * 1024;
	private const long MaxOpenXmlXmlPartBytes = 5L * 1024 * 1024;
	private const string ContentTypesNamespace = "http://schemas.openxmlformats.org/package/2006/content-types";
	private const string PackageRelationshipsNamespace = "http://schemas.openxmlformats.org/package/2006/relationships";
	private static readonly HashSet<string> OfficeDocumentRelationshipTypes = new(StringComparer.Ordinal)
	{
		"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument",
		"http://purl.oclc.org/ooxml/officeDocument/relationships/officeDocument"
	};

	private static readonly IReadOnlyDictionary<string, string[]> DocumentContentTypes =
		new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
		{
			[".pdf"] = ["application/pdf"],
			[".jpg"] = ["image/jpeg"],
			[".jpeg"] = ["image/jpeg"],
			[".png"] = ["image/png"],
			[".doc"] = ["application/msword"],
			[".docx"] = ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"],
			[".xls"] = ["application/vnd.ms-excel"],
			[".xlsx"] = ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"]
		};

	public static bool IsSupportedDocumentExtension(string extension) =>
		DocumentContentTypes.ContainsKey(NormalizeExtension(extension));

	public static bool IsMatchingDocumentContentType(string extension, string? contentType)
	{
		var normalizedExtension = NormalizeExtension(extension);
		var normalizedContentType = contentType?.Split(';', 2)[0].Trim();
		return DocumentContentTypes.TryGetValue(normalizedExtension, out var allowed) &&
		       !string.IsNullOrWhiteSpace(normalizedContentType) &&
		       allowed.Contains(normalizedContentType, StringComparer.OrdinalIgnoreCase);
	}
	/// <summary>
	/// Validates a profile picture upload: size, allowed extensions, and image magic bytes.
	/// </summary>
	public static void ValidateProfilePictureUpload(string fileName, byte[] fileContent)
	{
		if (string.IsNullOrWhiteSpace(fileName))
		{
			throw new BusinessException("File name cannot be empty.");
		}

		if (fileContent == null || fileContent.Length == 0)
		{
			throw new BusinessException("File content cannot be empty.");
		}

		const long maxFileSizeBytes = 5 * 1024 * 1024; // 5MB
		if (fileContent.Length > maxFileSizeBytes)
		{
			throw new BusinessException($"File size cannot exceed {maxFileSizeBytes / 1024.0 / 1024.0:F2}MB. Current size: {fileContent.Length / 1024.0 / 1024.0:F2}MB.");
		}

		var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
		var fileExtension = Path.GetExtension(fileName).ToLowerInvariant();

		if (string.IsNullOrEmpty(fileExtension) || !allowedExtensions.Contains(fileExtension))
		{
			throw new BusinessException($"Invalid file type. Allowed types: {string.Join(", ", allowedExtensions)}. Provided: {fileExtension}");
		}

		if (!IsValidImageFile(fileContent))
		{
			throw new BusinessException("Invalid image file. The file content does not match a valid image format.");
		}
	}

	/// <summary>
	/// Validates a document upload: size, allowed extensions, and magic-byte signature.
	/// </summary>
	public static void ValidateDocumentUpload(string fileName, byte[] fileContent, long size)
	{
		if (string.IsNullOrWhiteSpace(fileName))
		{
			throw new BusinessException("File name cannot be empty.");
		}

		var sanitizedFileName = Path.GetFileName(fileName);
		if (sanitizedFileName != fileName)
		{
			throw new BusinessException("File name contains invalid path characters.");
		}

		if (fileContent == null || fileContent.Length == 0)
		{
			throw new BusinessException("File content cannot be empty.");
		}

		const long maxFileSizeBytes = 10 * 1024 * 1024; // 10MB
		if (fileContent.Length > maxFileSizeBytes || size > maxFileSizeBytes)
		{
			throw new BusinessException($"File size cannot exceed {maxFileSizeBytes / 1024.0 / 1024.0:F0}MB. Current size: {fileContent.Length / 1024.0 / 1024.0:F2}MB.");
		}

		var fileExtension = Path.GetExtension(fileName).ToLowerInvariant();

		if (string.IsNullOrEmpty(fileExtension) || !IsSupportedDocumentExtension(fileExtension))
		{
			throw new BusinessException($"Invalid file type. Allowed types: {string.Join(", ", DocumentContentTypes.Keys)}. Provided: {fileExtension}");
		}

		ValidateDocumentFileSignature(fileContent, fileExtension);
	}

	/// <summary>
	/// Validates document file signature (magic bytes) to prevent file-type spoofing.
	/// </summary>
	public static void ValidateDocumentFileSignature(byte[] fileContent, string expectedExtension)
	{
		if (fileContent.Length < 4)
		{
			throw new BusinessException("File content is too small to be valid.");
		}

		// PDF: %PDF (25 50 44 46)
		if (expectedExtension == ".pdf")
		{
			if (!(fileContent[0] == 0x25 && fileContent[1] == 0x50 && fileContent[2] == 0x44 && fileContent[3] == 0x46))
			{
				throw new BusinessException("File extension is .pdf but content is not a valid PDF file.");
			}
		}

		// JPEG: FF D8 FF
		if (expectedExtension == ".jpg" || expectedExtension == ".jpeg")
		{
			if (!(fileContent[0] == 0xFF && fileContent[1] == 0xD8 && fileContent[2] == 0xFF))
			{
				throw new BusinessException("File extension is .jpg/.jpeg but content is not a valid JPEG file.");
			}
		}

		// PNG: 89 50 4E 47
		if (expectedExtension == ".png")
		{
			if (!(fileContent[0] == 0x89 && fileContent[1] == 0x50 && fileContent[2] == 0x4E && fileContent[3] == 0x47))
			{
				throw new BusinessException("File extension is .png but content is not a valid PNG file.");
			}
		}

		// DOCX/XLSX (ZIP format): 50 4B 03 04
		if (expectedExtension == ".docx" || expectedExtension == ".xlsx")
		{
			if (!(fileContent[0] == 0x50 && fileContent[1] == 0x4B && fileContent[2] == 0x03 && fileContent[3] == 0x04))
			{
				throw new BusinessException($"File extension is {expectedExtension} but content is not a valid Office document.");
			}

			ValidateOpenXmlPackage(fileContent, expectedExtension);
		}

		// DOC: D0 CF 11 E0
		if (expectedExtension == ".doc")
		{
			if (!HasOleCompoundFileSignature(fileContent))
			{
				throw new BusinessException("File extension is .doc but content is not a valid Word document.");
			}
		}

		// XLS: D0 CF 11 E0
		if (expectedExtension == ".xls")
		{
			if (!HasOleCompoundFileSignature(fileContent))
			{
				throw new BusinessException("File extension is .xls but content is not a valid Excel document.");
			}
		}
	}

	private static void ValidateOpenXmlPackage(byte[] fileContent, string expectedExtension)
	{
		try
		{
			using var stream = new MemoryStream(fileContent, writable: false);
			using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
			if (archive.Entries.Count == 0 || archive.Entries.Count > MaxOpenXmlEntryCount)
			{
				throw InvalidOfficePackage(
					expectedExtension,
					$"the package must contain between 1 and {MaxOpenXmlEntryCount} entries.");
			}

			var entryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var totalUncompressedBytes = 0L;
			foreach (var entry in archive.Entries)
			{
				ValidateOpenXmlEntryName(entry.FullName, expectedExtension);
				if (!entryNames.Add(entry.FullName))
				{
					throw InvalidOfficePackage(expectedExtension, "the package contains duplicate entry names.");
				}

				totalUncompressedBytes = checked(totalUncompressedBytes + entry.Length);
				if (totalUncompressedBytes > MaxOpenXmlUncompressedBytes)
				{
					throw InvalidOfficePackage(
						expectedExtension,
						$"the package expands beyond {MaxOpenXmlUncompressedBytes / 1024 / 1024}MB.");
				}
			}

			var requiredDocumentPart = expectedExtension == ".docx"
				? "word/document.xml"
				: "xl/workbook.xml";
			var contentTypesEntry = FindEntry(archive, "[Content_Types].xml");
			var rootRelationshipsEntry = FindEntry(archive, "_rels/.rels");
			var documentEntry = FindEntry(archive, requiredDocumentPart);
			if (contentTypesEntry is null || rootRelationshipsEntry is null || documentEntry is null)
			{
				throw InvalidOfficePackage(expectedExtension, "the package is missing required document parts.");
			}

			if (archive.Entries.Any(entry =>
				    entry.FullName.EndsWith("vbaProject.bin", StringComparison.OrdinalIgnoreCase)))
			{
				throw InvalidOfficePackage(expectedExtension, "macro-enabled content is not allowed.");
			}

			var contentTypes = LoadOpenXmlPart(contentTypesEntry, expectedExtension);
			ValidateOpenXmlContentTypes(contentTypes, requiredDocumentPart, expectedExtension);

			var rootRelationships = LoadOpenXmlPart(rootRelationshipsEntry, expectedExtension);
			ValidateOpenXmlRootRelationship(rootRelationships, requiredDocumentPart, expectedExtension);

			var document = LoadOpenXmlPart(documentEntry, expectedExtension);
			ValidateOpenXmlDocumentRoot(document, expectedExtension);
		}
		catch (Exception exception) when (
			exception is InvalidDataException or IOException or OverflowException or XmlException)
		{
			throw InvalidOfficePackage(
				expectedExtension,
				"content is not a valid Office package.",
				exception);
		}
	}

	private static void ValidateOpenXmlEntryName(string entryName, string expectedExtension)
	{
		if (string.IsNullOrWhiteSpace(entryName) ||
		    entryName.StartsWith("/", StringComparison.Ordinal) ||
		    entryName.Contains('\\'))
		{
			throw InvalidOfficePackage(expectedExtension, "the package contains an invalid entry name.");
		}

		var path = entryName.EndsWith("/", StringComparison.Ordinal)
			? entryName[..^1]
			: entryName;
		var segments = path.Split('/');
		if (segments.Length == 0 ||
		    segments.Any(segment =>
			    string.IsNullOrWhiteSpace(segment) ||
			    segment is "." or ".."))
		{
			throw InvalidOfficePackage(expectedExtension, "the package contains an unsafe entry path.");
		}
	}

	private static ZipArchiveEntry? FindEntry(ZipArchive archive, string entryName) =>
		archive.Entries.FirstOrDefault(entry =>
			string.Equals(entry.FullName, entryName, StringComparison.OrdinalIgnoreCase));

	private static XDocument LoadOpenXmlPart(
		ZipArchiveEntry entry,
		string expectedExtension)
	{
		if (entry.Length <= 0 || entry.Length > MaxOpenXmlXmlPartBytes)
		{
			throw InvalidOfficePackage(
				expectedExtension,
				$"XML part {entry.FullName} has an invalid size.");
		}

		using var partStream = entry.Open();
		using var reader = XmlReader.Create(partStream, new XmlReaderSettings
		{
			DtdProcessing = DtdProcessing.Prohibit,
			XmlResolver = null,
			MaxCharactersInDocument = MaxOpenXmlXmlPartBytes,
			IgnoreComments = true,
			IgnoreProcessingInstructions = true
		});
		return XDocument.Load(reader, LoadOptions.None);
	}

	private static void ValidateOpenXmlContentTypes(
		XDocument document,
		string requiredDocumentPart,
		string expectedExtension)
	{
		XNamespace contentTypesNamespace = ContentTypesNamespace;
		if (document.Root?.Name != contentTypesNamespace + "Types")
		{
			throw InvalidOfficePackage(expectedExtension, "the content-types manifest is invalid.");
		}

		var expectedContentType = expectedExtension == ".docx"
			? "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"
			: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml";
		var requiredPartName = $"/{requiredDocumentPart}";
		var hasRequiredOverride = document.Root
			.Elements(contentTypesNamespace + "Override")
			.Any(element =>
				string.Equals(
					element.Attribute("PartName")?.Value,
					requiredPartName,
					StringComparison.Ordinal) &&
				string.Equals(
					element.Attribute("ContentType")?.Value,
					expectedContentType,
					StringComparison.Ordinal));
		if (!hasRequiredOverride)
		{
			throw InvalidOfficePackage(
				expectedExtension,
				"the content-types manifest does not identify the expected main document part.");
		}

		var containsMacroContent = document.Root
			.Descendants()
			.Select(element => element.Attribute("ContentType")?.Value)
			.Where(value => !string.IsNullOrWhiteSpace(value))
			.Any(value =>
				value!.Contains("macroEnabled", StringComparison.OrdinalIgnoreCase) ||
				value.Contains("vbaProject", StringComparison.OrdinalIgnoreCase));
		if (containsMacroContent)
		{
			throw InvalidOfficePackage(expectedExtension, "macro-enabled content is not allowed.");
		}
	}

	private static void ValidateOpenXmlRootRelationship(
		XDocument document,
		string requiredDocumentPart,
		string expectedExtension)
	{
		XNamespace relationshipsNamespace = PackageRelationshipsNamespace;
		if (document.Root?.Name != relationshipsNamespace + "Relationships")
		{
			throw InvalidOfficePackage(expectedExtension, "the package relationship manifest is invalid.");
		}

		var officeDocumentRelationships = document.Root
			.Elements(relationshipsNamespace + "Relationship")
			.Where(element =>
				OfficeDocumentRelationshipTypes.Contains(element.Attribute("Type")?.Value ?? string.Empty))
			.ToList();
		if (officeDocumentRelationships.Count != 1)
		{
			throw InvalidOfficePackage(
				expectedExtension,
				"the package must contain one root office-document relationship.");
		}

		var relationship = officeDocumentRelationships[0];
		var targetMode = relationship.Attribute("TargetMode")?.Value;
		var target = NormalizeOpenXmlTarget(relationship.Attribute("Target")?.Value);
		if (string.Equals(targetMode, "External", StringComparison.OrdinalIgnoreCase) ||
		    !string.Equals(target, requiredDocumentPart, StringComparison.Ordinal))
		{
			throw InvalidOfficePackage(
				expectedExtension,
				"the root relationship does not target the expected document part.");
		}
	}

	private static string? NormalizeOpenXmlTarget(string? target)
	{
		if (string.IsNullOrWhiteSpace(target) || target.Contains('\\'))
		{
			return null;
		}

		var normalized = target.Trim().TrimStart('/');
		while (normalized.StartsWith("./", StringComparison.Ordinal))
		{
			normalized = normalized[2..];
		}

		return normalized
			.Split('/')
			.Any(segment => string.IsNullOrWhiteSpace(segment) || segment is "." or "..")
			? null
			: normalized;
	}

	private static void ValidateOpenXmlDocumentRoot(
		XDocument document,
		string expectedExtension)
	{
		var allowedRoots = expectedExtension == ".docx"
			? new HashSet<XName>
			{
				XName.Get("document", "http://schemas.openxmlformats.org/wordprocessingml/2006/main"),
				XName.Get("document", "http://purl.oclc.org/ooxml/wordprocessingml/main")
			}
			: new HashSet<XName>
			{
				XName.Get("workbook", "http://schemas.openxmlformats.org/spreadsheetml/2006/main"),
				XName.Get("workbook", "http://purl.oclc.org/ooxml/spreadsheetml/main")
			};
		if (document.Root is null || !allowedRoots.Contains(document.Root.Name))
		{
			throw InvalidOfficePackage(
				expectedExtension,
				"the main document part has an unexpected XML root.");
		}
	}

	private static BusinessException InvalidOfficePackage(
		string expectedExtension,
		string reason,
		Exception? innerException = null) =>
		new(
			message: $"File extension is {expectedExtension} but {reason}",
			innerException: innerException);

	private static bool HasOleCompoundFileSignature(byte[] fileContent) =>
		fileContent.Length >= 8 &&
		fileContent[0] == 0xD0 &&
		fileContent[1] == 0xCF &&
		fileContent[2] == 0x11 &&
		fileContent[3] == 0xE0 &&
		fileContent[4] == 0xA1 &&
		fileContent[5] == 0xB1 &&
		fileContent[6] == 0x1A &&
		fileContent[7] == 0xE1;

	/// <summary>
	/// Checks image magic bytes for JPEG, PNG, GIF, and WebP formats.
	/// </summary>
	public static bool IsValidImageFile(byte[] fileContent)
	{
		if (fileContent.Length < 4)
		{
			return false;
		}

		// JPEG: FF D8 FF
		if (fileContent[0] == 0xFF && fileContent[1] == 0xD8 && fileContent[2] == 0xFF)
		{
			return true;
		}

		// PNG: 89 50 4E 47
		if (fileContent.Length >= 8 &&
		    fileContent[0] == 0x89 && fileContent[1] == 0x50 &&
		    fileContent[2] == 0x4E && fileContent[3] == 0x47 &&
		    fileContent[4] == 0x0D && fileContent[5] == 0x0A &&
		    fileContent[6] == 0x1A && fileContent[7] == 0x0A)
		{
			return true;
		}

		// GIF: 47 49 46 38
		if (fileContent[0] == 0x47 && fileContent[1] == 0x49 && fileContent[2] == 0x46 && fileContent[3] == 0x38)
		{
			return true;
		}

		// WebP: RIFF....WEBP
		if (fileContent.Length >= 12 &&
		    fileContent[0] == 0x52 && fileContent[1] == 0x49 && fileContent[2] == 0x46 && fileContent[3] == 0x46 && // RIFF
		    fileContent[8] == 0x57 && fileContent[9] == 0x45 && fileContent[10] == 0x42 && fileContent[11] == 0x50) // WEBP
		{
			return true;
		}

		return false;
	}

	private static string NormalizeExtension(string extension) =>
		string.IsNullOrWhiteSpace(extension)
			? string.Empty
			: "." + extension.Trim().TrimStart('.').ToLowerInvariant();
}
