using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Volo.Abp;

namespace ShikshaMitra.Academic.Reports;

internal static class AcademicDynamicReportFilterResolver
{
	internal static AcademicDynamicReportSpecDto Resolve(
		string prompt,
		AcademicDynamicReportSpecDto plan,
		AcademicDynamicReportCatalogSnapshot snapshot)
	{
		ArgumentNullException.ThrowIfNull(plan);
		ArgumentNullException.ThrowIfNull(snapshot);

		plan.Filters ??= new AcademicDynamicReportFilterDto();
		plan.InterpretedRequest = NormalizeInterpretedRequest(plan.InterpretedRequest, prompt);
		var requestedTypes = (plan.RequestedFilterTypes ?? [])
			.Where(type => AcademicDynamicFilterTypes.All.Contains(type, StringComparer.OrdinalIgnoreCase))
			.Select(type => type.ToLowerInvariant())
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
		var searchableText = $"{prompt}\n{plan.InterpretedRequest}";

		ResolveFilter(
			AcademicDynamicFilterTypes.Class,
			"Classes",
			plan.Filters.ClassTokens,
			snapshot.Classes,
			item => item.Token,
			item => item.Name,
			searchableText,
			requestedTypes);
		ResolveFilter(
			AcademicDynamicFilterTypes.Division,
			"Divisions",
			plan.Filters.DivisionTokens,
			snapshot.Divisions,
			item => item.Token,
			item => item.Name,
			searchableText,
			requestedTypes);
		ResolveFilter(
			AcademicDynamicFilterTypes.Subject,
			"Subjects",
			plan.Filters.SubjectTokens,
			snapshot.Subjects,
			item => item.Token,
			item => item.Name,
			searchableText,
			requestedTypes);
		ResolveFilter(
			AcademicDynamicFilterTypes.Chapter,
			"Chapters",
			plan.Filters.ChapterTokens,
			snapshot.Chapters,
			item => item.Token,
			item => item.Name,
			searchableText,
			requestedTypes);

		plan.RequestedFilterTypes = requestedTypes
			.OrderBy(type => Array.IndexOf(AcademicDynamicFilterTypes.All, type))
			.ToList();
		plan.AppliedFilters = BuildAppliedFilters(plan.Filters, snapshot);
		return plan;
	}

	private static void ResolveFilter<T>(
		string filterType,
		string label,
		List<string> selectedTokens,
		IEnumerable<T> catalog,
		Func<T, string> tokenSelector,
		Func<T, string> nameSelector,
		string searchableText,
		ISet<string> requestedTypes)
	{
		var catalogItems = catalog.ToList();
		var explicitMatches = catalogItems
			.Where(item => HasQualifiedMention(searchableText, nameSelector(item), filterType))
			.ToList();
		var modelDeclaredFilter = requestedTypes.Contains(filterType);
		var translatedMatches = modelDeclaredFilter
			? catalogItems.Where(item => ContainsUnambiguousName(searchableText, nameSelector(item))).ToList()
			: [];
		var deterministicMatches = explicitMatches
			.Concat(translatedMatches)
			.GroupBy(tokenSelector, StringComparer.OrdinalIgnoreCase)
			.Select(group => group.First())
			.ToList();

		if (deterministicMatches.Count > 0)
		{
			selectedTokens.Clear();
			selectedTokens.AddRange(deterministicMatches.Select(tokenSelector));
			requestedTypes.Add(filterType);
		}

		selectedTokens.RemoveAll(string.IsNullOrWhiteSpace);
		var distinctTokens = selectedTokens.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
		selectedTokens.Clear();
		selectedTokens.AddRange(distinctTokens);

		if (requestedTypes.Contains(filterType) && selectedTokens.Count == 0)
		{
			throw new BusinessException("Academic:Reports:AiFilterCouldNotBeResolved")
				.WithData("FilterType", label);
		}
	}

	private static List<AcademicDynamicReportAppliedFilterDto> BuildAppliedFilters(
		AcademicDynamicReportFilterDto filters,
		AcademicDynamicReportCatalogSnapshot snapshot)
	{
		var applied = new List<AcademicDynamicReportAppliedFilterDto>();
		AddAppliedFilter(applied, AcademicDynamicFilterTypes.Class, "Classes", filters.ClassTokens,
			snapshot.Classes, item => item.Token, item => item.Name);
		AddAppliedFilter(applied, AcademicDynamicFilterTypes.Division, "Divisions", filters.DivisionTokens,
			snapshot.Divisions, item => item.Token, item => item.Name);
		AddAppliedFilter(applied, AcademicDynamicFilterTypes.Subject, "Subjects", filters.SubjectTokens,
			snapshot.Subjects, item => item.Token, item => item.Name);
		AddAppliedFilter(applied, AcademicDynamicFilterTypes.Chapter, "Chapters", filters.ChapterTokens,
			snapshot.Chapters, item => item.Token, item => item.Name);
		return applied;
	}

	private static void AddAppliedFilter<T>(
		ICollection<AcademicDynamicReportAppliedFilterDto> applied,
		string type,
		string label,
		IEnumerable<string> selectedTokens,
		IEnumerable<T> catalog,
		Func<T, string> tokenSelector,
		Func<T, string> nameSelector)
	{
		var tokenSet = selectedTokens.ToHashSet(StringComparer.OrdinalIgnoreCase);
		var values = catalog
			.Where(item => tokenSet.Contains(tokenSelector(item)))
			.Select(nameSelector)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
			.ToList();
		if (values.Count == 0) return;

		applied.Add(new AcademicDynamicReportAppliedFilterDto
		{
			Type = type,
			Label = label,
			Values = values
		});
	}

	private static bool HasQualifiedMention(string text, string catalogName, string filterType)
	{
		var normalizedText = NormalizeForMatch(text);
		var normalizedName = NormalizeForMatch(catalogName);
		if (string.IsNullOrWhiteSpace(normalizedName)) return false;

		if (filterType == AcademicDynamicFilterTypes.Class && normalizedName.StartsWith("class ", StringComparison.Ordinal))
		{
			return ContainsPhrase(normalizedText, normalizedName);
		}

		var qualifiers = filterType switch
		{
			AcademicDynamicFilterTypes.Class => new[] { "class", "classes", "grade", "grades", "standard", "standards" },
			AcademicDynamicFilterTypes.Division => new[] { "division", "divisions", "section", "sections" },
			AcademicDynamicFilterTypes.Subject => new[] { "subject", "subjects" },
			AcademicDynamicFilterTypes.Chapter => new[] { "chapter", "chapters", "unit", "units" },
			_ => []
		};
		return qualifiers.Any(qualifier =>
			ContainsPhrase(normalizedText, $"{normalizedName} {qualifier}") ||
			ContainsPhrase(normalizedText, $"{qualifier} {normalizedName}"));
	}

	private static bool ContainsUnambiguousName(string text, string catalogName)
	{
		var normalizedName = NormalizeForMatch(catalogName);
		return normalizedName.Length >= 3 && ContainsPhrase(NormalizeForMatch(text), normalizedName);
	}

	private static bool ContainsPhrase(string normalizedText, string normalizedPhrase) =>
		$" {normalizedText} ".Contains($" {normalizedPhrase} ", StringComparison.Ordinal);

	private static string NormalizeInterpretedRequest(string interpretedRequest, string prompt)
	{
		var value = Regex.Replace(
			string.IsNullOrWhiteSpace(interpretedRequest) ? prompt?.Trim() ?? string.Empty : interpretedRequest.Trim(),
			@"\s+",
			" ");
		return value.Length <= 500 ? value : value[..500];
	}

	private static string NormalizeForMatch(string value)
	{
		var builder = new StringBuilder(value?.Length ?? 0);
		var previousWasSpace = true;
		foreach (var character in (value ?? string.Empty).Normalize(NormalizationForm.FormKC))
		{
			if (char.IsLetterOrDigit(character))
			{
				builder.Append(char.ToLowerInvariant(character));
				previousWasSpace = false;
			}
			else if (!previousWasSpace)
			{
				builder.Append(' ');
				previousWasSpace = true;
			}
		}
		return builder.ToString().Trim();
	}
}
