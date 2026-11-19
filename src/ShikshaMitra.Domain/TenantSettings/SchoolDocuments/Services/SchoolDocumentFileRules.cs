using System;
using System.IO;
using System.Linq;
using Volo.Abp;

namespace ShikshaMitra.TenantSettings.SchoolDocuments;

public static class SchoolDocumentFileRules
{
	public static (string FileName, string ContentType) Validate(string fileName, string contentType, byte[] content)
	{
		var safeName = Path.GetFileName(fileName.Replace('\\', '/'));
		if (string.IsNullOrWhiteSpace(safeName) || safeName.Length > 256 || safeName.Any(char.IsControl) ||
			content.Length == 0 || content.Length > SchoolDocumentConsts.MaxFileBytes)
			throw new BusinessException(SchoolDocumentConsts.InvalidFile);
		var expected = Path.GetExtension(safeName).ToLowerInvariant() switch
		{
			".pdf" when content.AsSpan().StartsWith("%PDF-"u8) => "application/pdf",
			".png" when content.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) => "image/png",
			".jpg" or ".jpeg" when content.AsSpan().StartsWith(new byte[] { 255, 216, 255 }) => "image/jpeg",
			_ => throw new BusinessException(SchoolDocumentConsts.InvalidFile)
		};
		if (!string.Equals(expected, contentType, StringComparison.OrdinalIgnoreCase))
			throw new BusinessException(SchoolDocumentConsts.InvalidFile);
		return (safeName, expected);
	}

	public static void ValidateCount(int count)
	{
		if (count is < 1 or > SchoolDocumentConsts.MaxFiles)
			throw new BusinessException(SchoolDocumentConsts.InvalidFiles);
	}
}
