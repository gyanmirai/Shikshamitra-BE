using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;

namespace ShikshaMitra.Library.Catalogue;

public static partial class LibraryCatalogueRules
{
	public static LibraryCatalogueMetadata ValidateMetadata(LibraryCatalogueDefinition definition,
		LibraryCatalogueMetadata input, LibraryCatalogueMetadata? previous = null)
	{
		var effective = ValidateDefinition(definition);
		var languageCode = EmptyToNull(input.LanguageCode);
		var materialCode = EmptyToNull(input.MaterialCode);
		var legacyCode = LegacyCode(input.LegacyLanguage);
		var previousLanguageCode = previous == null ? null : EmptyToNull(previous.LanguageCode) ?? LegacyCode(previous.LegacyLanguage);
		if (input.LegacyLanguage.HasValue && legacyCode == null &&
			(previous == null || input.LegacyLanguage != previous.LegacyLanguage || languageCode != EmptyToNull(previous.LanguageCode)))
			throw InvalidMetadata("language", "Choose a configured language.");
		if (languageCode != null && input.LegacyLanguage.HasValue && languageCode != legacyCode)
			throw InvalidMetadata("language", "The selected language code and recorded numeric language disagree.");
		var effectiveCode = languageCode ?? legacyCode;
		ValidateChoice(effective.Languages, effectiveCode, previousLanguageCode, "language");
		ValidateChoice(effective.Materials, materialCode, EmptyToNull(previous?.MaterialCode), "material");
		var values = new Dictionary<string, string?>
		{
			["author"] = input.Author, ["isbn"] = input.Isbn, ["publisher"] = input.Publisher,
			["edition"] = input.Edition, ["subject"] = input.Subject, ["applicableAge"] = input.ApplicableAge,
			["language"] = effectiveCode ?? (input.LegacyLanguage.HasValue ? input.LegacyLanguage.Value.ToString() : null),
			["material"] = materialCode
		};
		foreach (var field in effective.RequiredFields)
			if (string.IsNullOrWhiteSpace(values[field])) throw InvalidMetadata(field, $"The school requires {field}.");
		foreach (var (field, maximum) in new[] { ("author", 256), ("isbn", 20), ("publisher", 256),
			("edition", 50), ("subject", 128), ("applicableAge", 10) })
			if (values[field]?.Trim().Length > maximum) throw InvalidMetadata(field, $"{field} supports up to {maximum} characters.");
		var builtInIndex = effectiveCode == null ? -1 : Array.IndexOf(LanguageCodes, effectiveCode);
		return input with
		{
			LanguageCode = languageCode,
			LegacyLanguage = builtInIndex >= 0 ? (Language)builtInIndex : input.LegacyLanguage,
			MaterialCode = materialCode
		};
	}

	private static void ValidateChoice(List<LibraryCatalogueChoice> choices, string? code, string? previous, string field)
	{
		if (code == null) return;
		var choice = choices.FirstOrDefault(item => string.Equals(item.Code, code, StringComparison.Ordinal));
		if ((choice == null || !choice.IsActive) && !string.Equals(code, previous, StringComparison.Ordinal))
			throw InvalidMetadata(field, "Choose an active school catalogue option.");
	}

	private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
	private static BusinessException InvalidMetadata(string field, string message) =>
		new BusinessException(LibraryErrorCodes.CatalogueMetadataInvalid, message).WithData("Field", field);
}
