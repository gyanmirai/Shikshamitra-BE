using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ShikshaMitra.Academic.Reports;

internal static class AcademicAiReportPlanNormalizer
{
	private static readonly IReadOnlyDictionary<string, string> ColumnAliases =
		new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			["classname"] = "class",
			["standard"] = "class",
			["standardname"] = "class",
			["subjectname"] = "subject",
			["primaryteachername"] = "primaryTeacher",
			["chapter"] = "chapterName",
			["teacher"] = "teacherName"
		};

	internal static AcademicDynamicReportSpecDto Normalize(
		string prompt,
		string allowedModule,
		AcademicDynamicReportSpecDto plan,
		IReadOnlyList<AcademicDynamicDatasetDefinition> datasets)
	{
		ArgumentNullException.ThrowIfNull(plan);
		ArgumentNullException.ThrowIfNull(datasets);

		if (string.IsNullOrWhiteSpace(plan.Module))
		{
			plan.Module = allowedModule;
		}

		plan.Columns = (plan.Columns ?? [])
			.Where(column => !string.IsNullOrWhiteSpace(column))
			.Select(column => ResolveColumnId(column, datasets) ?? column.Trim())
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();

		var requestsPrimaryTeacherBySubject =
			Contains(prompt, @"\bsubjects?\b") && Contains(prompt, @"\bprimary\s+teachers?\b");
		if (requestsPrimaryTeacherBySubject)
		{
			plan.Columns = plan.Columns
				.Select(column => string.Equals(column, "teacherName", StringComparison.OrdinalIgnoreCase)
					? "primaryTeacher"
					: column)
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList();

			if (RequestsOnlySubjectAndPrimaryTeacher(prompt))
			{
				plan.Columns = ["subject", "primaryTeacher"];
			}
		}

		var selectedDataset = ResolveDataset(plan.Dataset, datasets);
		if (requestsPrimaryTeacherBySubject)
		{
			selectedDataset = datasets.FirstOrDefault(dataset =>
				string.Equals(dataset.Id, AcademicDynamicDatasets.CurriculumSubjects, StringComparison.OrdinalIgnoreCase));
		}

		if (selectedDataset is null || !SupportsColumns(selectedDataset, plan.Columns))
		{
			var compatibleDatasets = datasets
				.Where(dataset => SupportsColumns(dataset, plan.Columns))
				.ToList();
			selectedDataset = compatibleDatasets.Count == 1
				? compatibleDatasets[0]
				: InferDatasetFromPrompt(prompt, datasets);
		}

		if (selectedDataset is not null)
		{
			plan.Dataset = selectedDataset.Id;
			var allowedColumns = selectedDataset.Columns
				.Select(column => column.Id)
				.ToHashSet(StringComparer.OrdinalIgnoreCase);
			plan.Sort = (plan.Sort ?? [])
				.Select(sort =>
				{
					sort.Field = ResolveColumnId(sort.Field, [selectedDataset]) ?? sort.Field?.Trim() ?? string.Empty;
					return sort;
				})
				.Where(sort => allowedColumns.Contains(sort.Field))
				.ToList();
		}

		return plan;
	}

	private static AcademicDynamicDatasetDefinition? ResolveDataset(
		string? requestedDataset,
		IReadOnlyList<AcademicDynamicDatasetDefinition> datasets)
	{
		if (string.IsNullOrWhiteSpace(requestedDataset)) return null;

		var requestedKey = NormalizeKey(requestedDataset);
		return datasets.FirstOrDefault(dataset =>
			string.Equals(dataset.Id, requestedDataset.Trim(), StringComparison.OrdinalIgnoreCase) ||
			NormalizeKey(dataset.Id) == requestedKey ||
			NormalizeKey(dataset.Name) == requestedKey ||
			NormalizeKey(dataset.Id.Split('.').Last()) == requestedKey);
	}

	private static string? ResolveColumnId(
		string? requestedColumn,
		IReadOnlyList<AcademicDynamicDatasetDefinition> datasets)
	{
		if (string.IsNullOrWhiteSpace(requestedColumn)) return null;

		var requestedKey = NormalizeKey(requestedColumn);
		if (ColumnAliases.TryGetValue(requestedKey, out var alias))
		{
			requestedKey = NormalizeKey(alias);
		}

		var matches = datasets
			.SelectMany(dataset => dataset.Columns)
			.Where(column =>
				string.Equals(column.Id, requestedColumn.Trim(), StringComparison.OrdinalIgnoreCase) ||
				NormalizeKey(column.Id) == requestedKey ||
				NormalizeKey(column.Label) == requestedKey)
			.Select(column => column.Id)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();

		return matches.Count == 1 ? matches[0] : null;
	}

	private static AcademicDynamicDatasetDefinition? InferDatasetFromPrompt(
		string prompt,
		IReadOnlyList<AcademicDynamicDatasetDefinition> datasets)
	{
		string? datasetId = null;
		if (Contains(prompt, @"\bchapters?\b|\bunits?\b|\bchapter\s+coverage\b"))
		{
			datasetId = AcademicDynamicDatasets.CurriculumChapters;
		}
		else if (Contains(prompt, @"\b(all|assigned|secondary)\s+teachers?\b|\bteacher\s+(type|role|code)\b"))
		{
			datasetId = AcademicDynamicDatasets.CurriculumTeachers;
		}
		else if (Contains(prompt, @"\bsubjects?\b|\bcurriculum\b|\bsyllabus\b"))
		{
			datasetId = AcademicDynamicDatasets.CurriculumSubjects;
		}

		return datasetId is null
			? null
			: datasets.FirstOrDefault(dataset => string.Equals(dataset.Id, datasetId, StringComparison.OrdinalIgnoreCase));
	}

	private static bool SupportsColumns(AcademicDynamicDatasetDefinition dataset, IReadOnlyCollection<string> columns)
	{
		if (columns.Count == 0) return true;
		var available = dataset.Columns.Select(column => column.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
		return columns.All(available.Contains);
	}

	private static bool Contains(string value, string pattern) =>
		Regex.IsMatch(value ?? string.Empty, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	private static bool RequestsOnlySubjectAndPrimaryTeacher(string value) =>
		Contains(value, @"\bonly\b[^.]{0,80}\b(two|2)\s+columns?\b") ||
		Contains(value, @"\b(?:each\s+)?subjects?(?:\s+names?)?\s+(?:and|with)\s+(?:its\s+|their\s+)?primary\s+teachers?\s+only\b") ||
		Contains(value, @"\bonly\s+(?:the\s+)?(?:subjects?(?:\s+names?)?)\s+(?:and|with)\s+(?:the\s+)?primary\s+teachers?\b");

	private static string NormalizeKey(string value) =>
		Regex.Replace(value, "[^a-zA-Z0-9]", string.Empty).ToLowerInvariant();
}
