using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Volo.Abp;

namespace ShikshaMitra.Library.Catalogue;

public static partial class LibraryCatalogueRules
{
	public const string DefaultsRevision = "defaults-v1";

	private static readonly string[] Fields = ["author", "isbn", "publisher", "edition", "subject", "language", "applicableAge", "material"];
	private static readonly string[] LanguageCodes = ["gu", "hi", "en", "mr", "sa"];

	public static LibraryCatalogueDefinition Defaults() => new()
	{
		RequiredFields = ["author", "language"],
		Languages = [new() { Code = "gu", Label = "Gujarati" }, new() { Code = "hi", Label = "Hindi" },
			new() { Code = "en", Label = "English" }, new() { Code = "mr", Label = "Marathi" }, new() { Code = "sa", Label = "Sanskrit" }],
		Materials = []
	};

	public static LibraryCatalogueDefinition ValidateDefinition(LibraryCatalogueDefinition definition,
		LibraryCatalogueDefinition? previous = null)
	{
		if (definition?.RequiredFields == null || definition.Languages == null || definition.Materials == null ||
			definition.RequiredFields.Count > Fields.Length || definition.RequiredFields.Distinct(StringComparer.Ordinal).Count() != definition.RequiredFields.Count ||
			definition.RequiredFields.Any(field => !Enumerable.Contains(Fields, field)))
			throw InvalidConfiguration("Choose each supported catalogue requirement at most once.");
		var result = new LibraryCatalogueDefinition
		{
			RequiredFields = definition.RequiredFields.ToList(),
			Languages = Choices(definition.Languages, previous?.Languages),
			Materials = Choices(definition.Materials, previous?.Materials)
		};
		if (result.RequiredFields.Contains("language") && !result.Languages.Any(choice => choice.IsActive) ||
			result.RequiredFields.Contains("material") && !result.Materials.Any(choice => choice.IsActive))
			throw InvalidConfiguration("A required language or material field needs at least one active choice.");
		return result;
	}

	public static string? LegacyCode(Language? language) =>
		language.HasValue && (int)language.Value >= 0 && (int)language.Value < LanguageCodes.Length
			? LanguageCodes[(int)language.Value] : null;

	private static List<LibraryCatalogueChoice> Choices(List<LibraryCatalogueChoice> choices, List<LibraryCatalogueChoice>? previous)
	{
		if (choices.Count > 200) throw InvalidConfiguration("A catalogue list supports up to 200 choices.");
		var codes = new HashSet<string>(StringComparer.Ordinal);
		var result = new List<LibraryCatalogueChoice>(choices.Count);
		foreach (var choice in choices)
		{
			if (choice == null || string.IsNullOrEmpty(choice.Code) || choice.Code.Length > 32 ||
				!Regex.IsMatch(choice.Code, @"\A[a-z][a-z0-9_-]{0,31}\z", RegexOptions.CultureInvariant) ||
				!codes.Add(choice.Code) || string.IsNullOrWhiteSpace(choice.Label) || choice.Label.Trim().Length > 128)
				throw InvalidConfiguration("Use unique lowercase choice codes and labels of up to 128 characters.");
			result.Add(new() { Code = choice.Code, Label = choice.Label.Trim(), IsActive = choice.IsActive });
		}
		if (previous != null && previous.Any(choice => !codes.Contains(choice.Code)))
			throw InvalidConfiguration("Retire an existing choice instead of removing its saved code.");
		return result;
	}

	private static BusinessException InvalidConfiguration(string message) => new(LibraryErrorCodes.CatalogueConfigurationInvalid, message);
}
