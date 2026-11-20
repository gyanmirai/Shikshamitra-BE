using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Library.Catalogue;

public class LibraryCatalogueLabelReader(LibraryCatalogueConfigurationReader configurations) : ITransientDependency
{
	public async Task<LibraryCatalogueLabels> ReadAsync() => new(await configurations.ReadAsync());
}

public sealed class LibraryCatalogueLabels(LibraryCatalogueDefinition definition)
{
	private readonly Dictionary<string, string> _languages = definition.Languages.ToDictionary(choice => choice.Code, choice => choice.Label, StringComparer.Ordinal);
	private readonly Dictionary<string, string> _materials = definition.Materials.ToDictionary(choice => choice.Code, choice => choice.Label, StringComparer.Ordinal);

	public string? LanguageCode(Book book) => book.LanguageCode ?? LibraryCatalogueRules.LegacyCode(book.Language);
	public string? LanguageLabel(Book book)
	{
		var code = LanguageCode(book);
		return code == null ? book.Language?.ToString() : _languages.GetValueOrDefault(code, code);
	}
	public string? MaterialLabel(Book book) => book.MaterialCode == null ? null : _materials.GetValueOrDefault(book.MaterialCode, book.MaterialCode);
}
