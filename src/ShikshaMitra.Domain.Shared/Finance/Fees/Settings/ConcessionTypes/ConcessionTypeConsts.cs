using ShikshaMitra.Common;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.Finance.Fees.Settings.ConcessionTypes;

public static class ConcessionTypeConsts
{
	public const int MaxNameLength = 100;
	public const int MaxDescriptionLength = 500;
	public const int MaxDeactivationReasonLength = 500;
	public const int MaxDocumentNameLength = 100;
	public const long DefaultMaxFileSizeBytes = 5L * 1024 * 1024;
	public const long AbsoluteMaxFileSizeBytes = 20L * 1024 * 1024;

	public static readonly IReadOnlyList<DocumentType> DefaultAcceptedFormats =
		[DocumentType.Pdf, DocumentType.Jpg, DocumentType.Jpeg, DocumentType.Png];

	public static string NormalizeDisplayName(string value) =>
		string.Join(' ', (value ?? string.Empty)
			.Trim()
			.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

	public static string NormalizeNameKey(string value) =>
		NormalizeDisplayName(value).ToUpperInvariant();

	public static string GetExtension(DocumentType documentType) => documentType switch
	{
		DocumentType.Jpg => ".jpg",
		DocumentType.Jpeg => ".jpeg",
		DocumentType.Png => ".png",
		DocumentType.Docx => ".docx",
		DocumentType.Pdf => ".pdf",
		DocumentType.Xlsx => ".xlsx",
		_ => throw new ArgumentOutOfRangeException(nameof(documentType), documentType, null)
	};

	public static DocumentType? GetDocumentType(string extension) =>
		DefaultExtensionMap.TryGetValue(extension.Trim().ToLowerInvariant(), out var documentType)
			? documentType
			: null;

	private static readonly IReadOnlyDictionary<string, DocumentType> DefaultExtensionMap =
		new Dictionary<string, DocumentType>(StringComparer.OrdinalIgnoreCase)
		{
			[".jpg"] = DocumentType.Jpg,
			[".jpeg"] = DocumentType.Jpeg,
			[".png"] = DocumentType.Png,
			[".docx"] = DocumentType.Docx,
			[".pdf"] = DocumentType.Pdf,
			[".xlsx"] = DocumentType.Xlsx
		};
}
