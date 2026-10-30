using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Localization;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.Localization.Json;
using Volo.Abp.Modularity;
using Volo.Abp.VirtualFileSystem;
using Xunit;

namespace ShikshaMitra.Localization;

public class ShikshaMitraLocalization_Tests : ShikshaMitraTestBase<ShikshaMitraLocalizationTestModule>
{
	[Fact]
	public void Development_Resources_Should_Have_One_Dictionary_Per_Culture()
	{
		var dictionaries = Directory.GetFiles(ShikshaMitraLocalizationTestModule.ResourceDirectory, "*.json")
			.Select(path => (Name: path, Json: File.ReadAllText(path)));
		AssertUniqueCultures(dictionaries);
	}

	[Fact]
	public void Embedded_Resources_Should_Have_One_Dictionary_Per_Culture()
	{
		var assembly = typeof(ShikshaMitraResource).Assembly;
		var dictionaries = assembly.GetManifestResourceNames()
			.Where(name => name.StartsWith("ShikshaMitra.Localization.ShikshaMitra.", StringComparison.Ordinal)
				&& name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
			.Select(name =>
			{
				using var reader = new StreamReader(assembly.GetManifestResourceStream(name)!);
				return (Name: name, Json: reader.ReadToEnd());
			});
		AssertUniqueCultures(dictionaries);
	}

	[Theory]
	[InlineData("en", "Permission:Student.AttendanceTracker", "Attendance Tracker")]
	[InlineData("en", "StudentAttendance:ConcurrencyConflict", "Attendance changed while you were editing. Refresh and review the latest record before saving.")]
	[InlineData("hi", "Menu:Home", "घर")]
	[InlineData("en", "Students:Family:MainContactInvalid", "Choose a saved parent or guardian as the main school contact. Review the family details before finalizing admission.")]
	[InlineData("hi", "Students:Family:MainContactInvalid", "स्कूल के मुख्य संपर्क के लिए सहेजे गए माता-पिता या अभिभावक को चुनें। प्रवेश अंतिम करने से पहले परिवार की जानकारी जाँचें।")]
	public void Development_Localizer_Should_Resolve_Permission_And_Existing_Translations(
		string culture, string key, string expected)
	{
		var previousCulture = CultureInfo.CurrentUICulture;
		try
		{
			CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
			var localizer = GetRequiredService<IStringLocalizer<ShikshaMitraResource>>();
			localizer[key].ResourceNotFound.ShouldBeFalse();
			localizer[key].Value.ShouldBe(expected);
		}
		finally
		{
			CultureInfo.CurrentUICulture = previousCulture;
		}
	}

	private static void AssertUniqueCultures(IEnumerable<(string Name, string Json)> resources)
	{
		var cultures = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (var (name, json) in resources)
		{
			var dictionary = JsonLocalizationDictionaryBuilder.BuildFromJsonString(json);
			if (dictionary == null)
				continue; // ABP ignores JSON files that do not declare a culture.

			cultures.TryGetValue(dictionary.CultureName, out var existing);
			cultures.TryAdd(dictionary.CultureName, name).ShouldBeTrue(
				$"Duplicate culture '{dictionary.CultureName}' in '{existing}' and '{name}'. Merge their texts into one dictionary.");
		}
		cultures.ShouldContainKey("en");
		cultures.ShouldContainKey("hi");
	}
}

[DependsOn(typeof(ShikshaMitraDomainSharedModule), typeof(AbpAutofacModule), typeof(AbpTestBaseModule))]
public class ShikshaMitraLocalizationTestModule : AbpModule
{
	internal static string ResourceDirectory => Path.Combine(DomainSharedDirectory, "Localization", "ShikshaMitra");

	private static string DomainSharedDirectory
	{
		get
		{
			for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
			{
				var path = Path.Combine(directory.FullName, "src", "ShikshaMitra.Domain.Shared");
				if (File.Exists(Path.Combine(path, "ShikshaMitra.Domain.Shared.csproj")))
					return path;
			}
			throw new DirectoryNotFoundException("Could not locate the Domain.Shared localization source directory.");
		}
	}

	public override void ConfigureServices(ServiceConfigurationContext context)
	{
		Configure<AbpVirtualFileSystemOptions>(options =>
			options.FileSets.ReplaceEmbeddedByPhysical<ShikshaMitraDomainSharedModule>(DomainSharedDirectory));
	}
}
