using System.IO;
using System.Linq;
using Volo.Abp;

namespace ShikshaMitra.Common;

/// <summary>
/// Static utility for validating file uploads — size, extension whitelist, and magic-byte checks.
/// Extracted as an infrastructure concern so it can be reused across domain services.
/// </summary>
public static class FileValidationService
{
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

		var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".doc", ".docx", ".xls", ".xlsx" };
		var fileExtension = Path.GetExtension(fileName).ToLowerInvariant();

		if (string.IsNullOrEmpty(fileExtension) || !allowedExtensions.Contains(fileExtension))
		{
			throw new BusinessException($"Invalid file type. Allowed types: {string.Join(", ", allowedExtensions)}. Provided: {fileExtension}");
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
		}

		// DOC: D0 CF 11 E0
		if (expectedExtension == ".doc")
		{
			if (fileContent.Length >= 8 &&
				!(fileContent[0] == 0xD0 && fileContent[1] == 0xCF && fileContent[2] == 0x11 && fileContent[3] == 0xE0))
			{
				throw new BusinessException("File extension is .doc but content is not a valid Word document.");
			}
		}

		// XLS: D0 CF 11 E0
		if (expectedExtension == ".xls")
		{
			if (fileContent.Length >= 8 &&
				!(fileContent[0] == 0xD0 && fileContent[1] == 0xCF && fileContent[2] == 0x11 && fileContent[3] == 0xE0))
			{
				throw new BusinessException("File extension is .xls but content is not a valid Excel document.");
			}
		}
	}

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
		if (fileContent[0] == 0x89 && fileContent[1] == 0x50 && fileContent[2] == 0x4E && fileContent[3] == 0x47)
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
}
