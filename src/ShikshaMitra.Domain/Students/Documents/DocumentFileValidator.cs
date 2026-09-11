using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using Volo.Abp;

namespace ShikshaMitra.Students.Documents;

/// <summary>Shared file checks for student and admission evidence uploads.</summary>
public static class DocumentFileValidator
{
	public static DocumentFileValidationResult Validate(
		string fileName, string contentType, long size, byte[] content, DocumentType documentType) =>
		Validate(fileName, contentType, size, content, documentType.Name,
			documentType.MaxSizeMB, documentType.GetAcceptedFormats());

	public static DocumentFileValidationResult Validate(
		string fileName,
		string contentType,
		long size,
		byte[] content,
		string documentTypeName,
		double maxSizeMB,
		IReadOnlyCollection<AcceptedFileFormat> acceptedFormats)
	{
		if (size <= 0 || content.Length == 0)
		{
			throw new BusinessException("Document:EmptyFile");
		}

		if (size != content.LongLength)
		{
			throw new BusinessException("Document:SizeMismatch");
		}

		ValidateFileSize(size, documentTypeName, maxSizeMB);

		var safeFileName = SanitizeFileName(fileName);
		var fileExtension = Path.GetExtension(safeFileName)?.ToLowerInvariant();
		if (string.IsNullOrWhiteSpace(fileExtension))
		{
			throw new BusinessException("Document:InvalidFileName");
		}

		var format = MapExtensionToFormat(fileExtension);
		if (!format.HasValue)
		{
			throw new BusinessException("Document:UnsupportedFileExtension")
				.WithData("Extension", fileExtension);
		}

		if (!acceptedFormats.Contains(format.Value))
		{
			throw new BusinessException("Document:InvalidFormat")
				.WithData("DocumentTypeName", documentTypeName)
				.WithData("ProvidedFormat", format.Value.ToString())
				.WithData("AcceptedFormats", string.Join(", ", acceptedFormats.Select(f => f.ToString())));
		}

		var expectedContentType = GetContentTypeForFormat(format.Value);
		if (!contentType.Equals(expectedContentType, StringComparison.OrdinalIgnoreCase))
		{
			throw new BusinessException("Document:ContentTypeMismatch")
				.WithData("ProvidedContentType", contentType)
				.WithData("ExpectedContentType", expectedContentType)
				.WithData("Extension", fileExtension);
		}

		if (!HasExpectedSignature(content, format.Value))
		{
			throw new BusinessException("Document:SignatureMismatch")
				.WithData("Extension", fileExtension)
				.WithData("ContentType", expectedContentType);
		}

		return new DocumentFileValidationResult(
			safeFileName,
			expectedContentType,
			ComputeSha256(content),
			IsSensitiveDocumentType(documentTypeName));
	}

	private static void ValidateFileSize(long sizeInBytes, string documentTypeName, double maxSizeMB)
	{
		var sizeInMB = sizeInBytes / (1024.0 * 1024.0);

		if (sizeInMB > maxSizeMB)
		{
			throw new BusinessException("Document:FileSizeExceeded")
				.WithData("ActualSizeMB", Math.Round(sizeInMB, 2))
				.WithData("MaxSizeMB", maxSizeMB)
				.WithData("DocumentTypeName", documentTypeName);
		}
	}

	public static string SanitizeFileName(string fileName)
	{
		var name = Path.GetFileName(fileName);
		var invalidChars = Path.GetInvalidFileNameChars();
		var safe = string.Join("_", name.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries)).Trim();

		return string.IsNullOrWhiteSpace(safe)
			? "document"
			: safe;
	}

	private static AcceptedFileFormat? MapExtensionToFormat(string extension)
	{
		return extension.ToLowerInvariant() switch
		{
			".jpg" => AcceptedFileFormat.Jpg,
			".jpeg" => AcceptedFileFormat.Jpeg,
			".png" => AcceptedFileFormat.Png,
			".pdf" => AcceptedFileFormat.Pdf,
			".docx" => AcceptedFileFormat.Docx,
			".xlsx" => AcceptedFileFormat.Xlsx,
			_ => null
		};
	}

	private static string GetContentTypeForFormat(AcceptedFileFormat format)
	{
		return format switch
		{
			AcceptedFileFormat.Jpg => "image/jpeg",
			AcceptedFileFormat.Jpeg => "image/jpeg",
			AcceptedFileFormat.Png => "image/png",
			AcceptedFileFormat.Pdf => "application/pdf",
			AcceptedFileFormat.Docx => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
			AcceptedFileFormat.Xlsx => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
			_ => throw new ArgumentException($"Unsupported format: {format}")
		};
	}

	private static bool HasExpectedSignature(byte[] content, AcceptedFileFormat format)
	{
		if (content.Length < 4)
		{
			return false;
		}

		return format switch
		{
			AcceptedFileFormat.Jpg or AcceptedFileFormat.Jpeg =>
				content.Length >= 3 && content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF,
			AcceptedFileFormat.Png =>
				content.Length >= 8 &&
				content[0] == 0x89 && content[1] == 0x50 && content[2] == 0x4E && content[3] == 0x47 &&
				content[4] == 0x0D && content[5] == 0x0A && content[6] == 0x1A && content[7] == 0x0A,
			AcceptedFileFormat.Pdf =>
				content[0] == 0x25 && content[1] == 0x50 && content[2] == 0x44 && content[3] == 0x46,
			AcceptedFileFormat.Docx or AcceptedFileFormat.Xlsx =>
				content[0] == 0x50 && content[1] == 0x4B && content[2] == 0x03 && content[3] == 0x04,
			_ => false
		};
	}

	private static string ComputeSha256(byte[] content)
	{
		var hash = SHA256.HashData(content);
		return Convert.ToHexString(hash).ToLowerInvariant();
	}

	private static bool IsSensitiveDocumentType(string documentTypeName)
	{
		var normalized = documentTypeName.ToLowerInvariant();
		return normalized.Contains("aadhaar", StringComparison.Ordinal) ||
		       normalized.Contains("aadhar", StringComparison.Ordinal) ||
		       normalized.Contains("caste", StringComparison.Ordinal) ||
		       normalized.Contains("rte", StringComparison.Ordinal) ||
		       normalized.Contains("scholarship", StringComparison.Ordinal);
	}

}

/// <summary>Validated metadata; callers keep the file bytes in private storage.</summary>
public sealed record DocumentFileValidationResult(
	string SafeFileName,
	string ContentType,
	string Sha256Hash,
	bool IsSensitive);
