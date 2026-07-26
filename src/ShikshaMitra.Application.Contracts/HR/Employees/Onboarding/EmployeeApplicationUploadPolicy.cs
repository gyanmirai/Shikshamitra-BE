using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ShikshaMitra.HR.Employees.Onboarding;

public enum EmployeeApplicationUploadKind
{
	Resume,
	Photo
}

public static class EmployeeApplicationUploadPolicy
{
	private const int MaxFileNameLength = 255;
	private static readonly char[] UnsafeFileNameCharacters = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];

	private static readonly IReadOnlyDictionary<string, UploadType> ResumeTypes =
		new Dictionary<string, UploadType>(StringComparer.OrdinalIgnoreCase)
		{
			[".pdf"] = new("application/pdf", "application/pdf"),
			[".doc"] = new("application/msword", "application/msword"),
			[".docx"] = new(
				"application/vnd.openxmlformats-officedocument.wordprocessingml.document",
				"application/vnd.openxmlformats-officedocument.wordprocessingml.document")
		};

	private static readonly IReadOnlyDictionary<string, UploadType> PhotoTypes =
		new Dictionary<string, UploadType>(StringComparer.OrdinalIgnoreCase)
		{
			[".jpg"] = new("image/jpeg", "image/jpeg", "image/jpg"),
			[".jpeg"] = new("image/jpeg", "image/jpeg", "image/jpg"),
			[".png"] = new("image/png", "image/png"),
			[".gif"] = new("image/gif", "image/gif"),
			[".webp"] = new("image/webp", "image/webp")
		};

	public static bool TryNormalize(
		string? fileName,
		string? suppliedContentType,
		EmployeeApplicationUploadKind kind,
		out string safeFileName,
		out string canonicalContentType)
	{
		safeFileName = string.Empty;
		canonicalContentType = string.Empty;
		if (!TryResolveUploadType(fileName, kind, out safeFileName, out var uploadType))
		{
			return false;
		}

		var normalizedContentType = NormalizeContentType(suppliedContentType);
		if (string.IsNullOrWhiteSpace(normalizedContentType) ||
		    !uploadType.AllowedContentTypes.Contains(normalizedContentType))
		{
			return false;
		}

		canonicalContentType = uploadType.CanonicalContentType;
		return true;
	}

	public static bool TryNormalizeStored(
		string? fileName,
		string? storedContentType,
		EmployeeApplicationUploadKind kind,
		out string safeFileName,
		out string canonicalContentType)
	{
		safeFileName = string.Empty;
		canonicalContentType = string.Empty;
		if (!TryResolveUploadType(fileName, kind, out safeFileName, out var uploadType))
		{
			return false;
		}

		var normalizedContentType = NormalizeContentType(storedContentType);
		if (!string.IsNullOrWhiteSpace(normalizedContentType) &&
		    !string.Equals(
			    normalizedContentType,
			    "application/octet-stream",
			    StringComparison.OrdinalIgnoreCase) &&
		    !uploadType.AllowedContentTypes.Contains(normalizedContentType))
		{
			return false;
		}

		canonicalContentType = uploadType.CanonicalContentType;
		return true;
	}

	public static string GetAllowedExtensions(EmployeeApplicationUploadKind kind) =>
		string.Join(
			", ",
			(kind == EmployeeApplicationUploadKind.Resume ? ResumeTypes : PhotoTypes).Keys);

	private static bool TryResolveUploadType(
		string? fileName,
		EmployeeApplicationUploadKind kind,
		out string safeFileName,
		out UploadType uploadType)
	{
		safeFileName = string.Empty;
		uploadType = null!;
		if (string.IsNullOrWhiteSpace(fileName) ||
		    fileName.Length > MaxFileNameLength ||
		    !string.Equals(fileName, fileName.Trim(), StringComparison.Ordinal) ||
		    fileName.Any(char.IsControl) ||
		    fileName.IndexOfAny(UnsafeFileNameCharacters) >= 0 ||
		    !string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal) ||
		    string.IsNullOrWhiteSpace(Path.GetFileNameWithoutExtension(fileName)))
		{
			return false;
		}

		var extension = Path.GetExtension(fileName);
		var allowedTypes = kind == EmployeeApplicationUploadKind.Resume ? ResumeTypes : PhotoTypes;
		if (!allowedTypes.TryGetValue(extension, out uploadType!))
		{
			return false;
		}

		safeFileName = fileName;
		return true;
	}

	private static string? NormalizeContentType(string? contentType) =>
		contentType?
			.Split(';', 2, StringSplitOptions.TrimEntries)[0]
			.Trim();

	private sealed class UploadType
	{
		public UploadType(string canonicalContentType, params string[] allowedContentTypes)
		{
			CanonicalContentType = canonicalContentType;
			AllowedContentTypes = new HashSet<string>(
				allowedContentTypes,
				StringComparer.OrdinalIgnoreCase);
		}

		public string CanonicalContentType { get; }
		public HashSet<string> AllowedContentTypes { get; }
	}
}
