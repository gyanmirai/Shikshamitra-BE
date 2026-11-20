using System;
using System.Linq;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Catalogue;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryCatalogueRules_Tests
{
	private static LibraryCatalogueMetadata Book(string? author = "Author", Language? language = Language.English,
		string? code = null, string? material = null) =>
		new(author, null, null, null, null, null, language, code, material);

	[Fact]
	public void Unconfigured_Schools_Should_Keep_Required_Author_And_The_Five_Existing_Language_Meanings()
	{
		var defaults = LibraryCatalogueRules.Defaults();
		Assert.Equal<string>(["author", "language"], defaults.RequiredFields);
		Assert.Equal<string>(["gu", "hi", "en", "mr", "sa"], defaults.Languages.Select(choice => choice.Code));
		Assert.Empty(defaults.Materials);
		Assert.Equal("gu", LibraryCatalogueRules.LegacyCode((Language)0));
		Assert.Equal("hi", LibraryCatalogueRules.LegacyCode((Language)1));
		Assert.Equal("en", LibraryCatalogueRules.LegacyCode((Language)2));
		Assert.Equal("mr", LibraryCatalogueRules.LegacyCode((Language)3));
		Assert.Equal("sa", LibraryCatalogueRules.LegacyCode((Language)4));
		Assert.Throws<BusinessException>(() => LibraryCatalogueRules.ValidateMetadata(defaults, Book(author: null)));
		Assert.Throws<BusinessException>(() => LibraryCatalogueRules.ValidateMetadata(defaults, Book(language: null)));
		Assert.Equal((Language)0, LibraryCatalogueRules.ValidateMetadata(defaults, Book(language: (Language)0)).LegacyLanguage);
	}

	[Fact]
	public void Allowing_Anonymous_Books_In_One_School_Should_Not_Change_Another_Schools_Defaults()
	{
		var primary = LibraryCatalogueRules.Defaults();
		primary.RequiredFields.Remove("author");
		var result = LibraryCatalogueRules.ValidateMetadata(primary, Book(author: null));
		Assert.Null(result.Author);
		Assert.Throws<BusinessException>(() => LibraryCatalogueRules.ValidateMetadata(LibraryCatalogueRules.Defaults(), Book(author: null)));
	}

	[Fact]
	public void A_Custom_Language_Should_Not_Be_Recorded_As_A_Built_In_Language()
	{
		var definition = LibraryCatalogueRules.Defaults();
		definition.Languages.Add(new() { Code = "bn", Label = "বাংলা" });
		var result = LibraryCatalogueRules.ValidateMetadata(definition, Book(language: null, code: "bn"));
		Assert.Equal("bn", result.LanguageCode);
		Assert.Null(result.LegacyLanguage);
		Assert.Throws<BusinessException>(() => LibraryCatalogueRules.ValidateMetadata(definition, Book(language: Language.Gujarati, code: "bn")));
	}

	[Fact]
	public void An_Optional_Unknown_Language_And_Material_Should_Remain_Unspecified()
	{
		var definition = LibraryCatalogueRules.Defaults();
		definition.RequiredFields.Remove("language");
		var result = LibraryCatalogueRules.ValidateMetadata(definition, Book(language: null));
		Assert.Null(result.LanguageCode);
		Assert.Null(result.LegacyLanguage);
		Assert.Null(result.MaterialCode);
	}

	[Fact]
	public void Retired_Choices_Should_Survive_Unrelated_Updates_But_Be_Unavailable_For_New_Selections()
	{
		var definition = LibraryCatalogueRules.Defaults();
		definition.Languages.Add(new() { Code = "bn", Label = "বাংলা", IsActive = false });
		definition.Materials.Add(new() { Code = "atlas", Label = "Atlas", IsActive = false });
		var old = Book(language: null, code: "bn", material: "atlas");
		Assert.Equal(old with { Author = "Corrected author" },
			LibraryCatalogueRules.ValidateMetadata(definition, old with { Author = "Corrected author" }, old));
		Assert.Throws<BusinessException>(() => LibraryCatalogueRules.ValidateMetadata(definition, old));
	}

	[Fact]
	public void A_Required_Material_Should_Be_Explicit_Rather_Than_Inferred_From_A_Category()
	{
		var definition = LibraryCatalogueRules.Defaults();
		definition.RequiredFields.Add("material");
		definition.Materials.Add(new() { Code = "book", Label = "Book" });
		Assert.Throws<BusinessException>(() => LibraryCatalogueRules.ValidateMetadata(definition, Book()));
		Assert.Equal("book", LibraryCatalogueRules.ValidateMetadata(definition, Book(material: "book")).MaterialCode);
	}

	[Theory]
	[InlineData("dueDate")]
	[InlineData("price")]
	[InlineData("author,author")]
	public void Configuration_Should_Reject_Unknown_Or_Duplicate_Requirements(string fields)
	{
		var definition = LibraryCatalogueRules.Defaults();
		definition.RequiredFields = [.. fields.Split(',')];
		Assert.Throws<BusinessException>(() => LibraryCatalogueRules.ValidateDefinition(definition));
	}

	[Fact]
	public void Existing_Choice_Codes_Should_Be_Retired_Instead_Of_Deleted()
	{
		var previous = LibraryCatalogueRules.Defaults();
		var changed = LibraryCatalogueRules.Defaults();
		changed.Languages.RemoveAt(0);
		Assert.Throws<BusinessException>(() => LibraryCatalogueRules.ValidateDefinition(changed, previous));
	}

	[Fact]
	public void Duplicate_Choice_Codes_And_Unsupported_Numeric_Languages_Should_Be_Rejected()
	{
		var definition = LibraryCatalogueRules.Defaults();
		definition.Languages.Add(new() { Code = "en", Label = "Duplicate English" });
		Assert.Throws<BusinessException>(() => LibraryCatalogueRules.ValidateDefinition(definition));
		Assert.Throws<BusinessException>(() => LibraryCatalogueRules.ValidateMetadata(LibraryCatalogueRules.Defaults(), Book(language: (Language)99)));
	}

	[Fact]
	public void A_Stale_Configuration_Revision_Should_Not_Overwrite_Another_Administrators_Save()
	{
		var configuration = new LibraryCatalogueConfiguration(Guid.NewGuid(), Guid.NewGuid(), LibraryCatalogueRules.Defaults());
		var revision = configuration.Revision;
		var change = configuration.Read();
		change.RequiredFields.Remove("author");
		configuration.Revise(change, revision);
		var saved = configuration.DefinitionJson;
		Assert.NotEqual(revision, configuration.Revision);
		Assert.Throws<BusinessException>(() => configuration.Revise(LibraryCatalogueRules.Defaults(), revision));
		Assert.Equal(saved, configuration.DefinitionJson);
		Assert.DoesNotContain("author", configuration.Read().RequiredFields);
	}

	[Fact]
	public void Returned_Configuration_And_Original_Inputs_Should_Not_Mutate_Persisted_Rules()
	{
		var definition = LibraryCatalogueRules.Defaults();
		var configuration = new LibraryCatalogueConfiguration(Guid.NewGuid(), Guid.NewGuid(), definition);
		definition.RequiredFields.Clear();
		var returned = configuration.Read();
		returned.Languages[0].Label = "Changed without saving";
		Assert.Contains("author", configuration.Read().RequiredFields);
		Assert.Equal("Gujarati", configuration.Read().Languages[0].Label);
	}
}
