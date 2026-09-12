using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Academic.Reports;

public class AcademicDynamicReportGuard(AcademicDynamicReportCatalog catalog) : ITransientDependency
{
	private static readonly IReadOnlyDictionary<string, string[]> ModuleTerms =
		new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
		{
			[AcademicReportModules.Homework] = ["homework", "home work"],
			[AcademicReportModules.Assignment] =
				["student assignment", "student assignments", "assignment report", "assignments report", "assignment submission", "rubric"],
			[AcademicReportModules.Examination] =
				["exam", "examination", "student marks", "exam marks", "result report", "student results", "grade analysis"],
			[AcademicReportModules.Timetable] = ["timetable", "time table", "class schedule", "room utilization"]
		};

	public void EnsureRequestAllowed(string prompt, string module)
	{
		module = NormalizeModule(module);
		if (!string.Equals(module, AcademicReportModules.Curriculum, StringComparison.OrdinalIgnoreCase))
		{
			throw new BusinessException("Academic:Reports:AiModuleNotAvailable")
				.WithData("Module", module);
		}

		if (Regex.IsMatch(prompt, @"(?i)\b(drop|truncate|alter)\s+(table|database)\b") ||
			Regex.IsMatch(prompt, @"(?i)\b(ignore|override)\s+(all\s+)?(previous|system|developer)\s+instructions?\b"))
		{
			throw new BusinessException("Academic:Reports:AiUnsafeRequest");
		}

		var requestedModule = DetectRequestedModule(prompt);
		if (requestedModule is not null)
		{
			throw ModuleScopeMismatch(module, requestedModule);
		}
	}

	public AcademicDynamicReportSpecDto ValidatePlan(
		AcademicDynamicReportSpecDto plan,
		string allowedModule,
		AcademicDynamicReportCatalogSnapshot snapshot)
	{
		ArgumentNullException.ThrowIfNull(plan);
		allowedModule = NormalizeModule(allowedModule);
		plan.Module = NormalizeModule(plan.Module);
		if (!string.Equals(plan.Module, allowedModule, StringComparison.OrdinalIgnoreCase))
		{
			throw ModuleScopeMismatch(allowedModule, plan.Module);
		}
		EnsureRequestAllowed($"{plan.Title} {plan.Summary}", allowedModule);

		var dataset = catalog.GetDataset(plan.Dataset?.Trim() ?? string.Empty);
		if (!string.Equals(dataset.Module, allowedModule, StringComparison.OrdinalIgnoreCase))
		{
			throw DatasetNotAllowed(plan.Dataset);
		}

		plan.Dataset = dataset.Id;
		plan.Title = NormalizeText(plan.Title, dataset.Name, 120);
		plan.Summary = NormalizeText(plan.Summary, $"Dynamic {dataset.Name.ToLowerInvariant()} report.", 400);
		plan.Columns = (plan.Columns ?? [])
			.Where(column => !string.IsNullOrWhiteSpace(column))
			.Select(column => column.Trim())
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();
		if (plan.Columns.Count == 0)
		{
			plan.Columns = dataset.DefaultColumns.ToList();
		}
		if (plan.Columns.Count > 20)
		{
			throw new BusinessException("Academic:Reports:AiTooManyColumns");
		}

		var availableColumns = dataset.Columns.Select(column => column.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
		if (plan.Columns.Any(column => !availableColumns.Contains(column)))
		{
			throw new BusinessException("Academic:Reports:AiColumnNotAllowed");
		}

		plan.Filters ??= new AcademicDynamicReportFilterDto();
		ValidateTokens(plan.Filters.ClassTokens, snapshot.Classes.Select(item => item.Token), "class");
		ValidateTokens(plan.Filters.DivisionTokens, snapshot.Divisions.Select(item => item.Token), "division");
		ValidateTokens(plan.Filters.SubjectTokens, snapshot.Subjects.Select(item => item.Token), "subject");
		ValidateTokens(plan.Filters.ChapterTokens, snapshot.Chapters.Select(item => item.Token), "chapter");

		plan.Sort = (plan.Sort ?? []).Take(3).ToList();
		foreach (var sort in plan.Sort)
		{
			sort.Field = sort.Field?.Trim() ?? string.Empty;
			if (!availableColumns.Contains(sort.Field))
			{
				throw new BusinessException("Academic:Reports:AiSortNotAllowed");
			}
			sort.Direction = string.Equals(sort.Direction, AcademicDynamicSortDirections.Descending, StringComparison.OrdinalIgnoreCase)
				? AcademicDynamicSortDirections.Descending
				: AcademicDynamicSortDirections.Ascending;
		}

		return plan;
	}

	public static string NormalizeModule(string? module) =>
		string.IsNullOrWhiteSpace(module) ? AcademicReportModules.Curriculum : module.Trim();

	public static BusinessException DatasetNotAllowed(string? dataset) =>
		new BusinessException("Academic:Reports:AiDatasetNotAllowed")
			.WithData("Dataset", dataset ?? string.Empty);

	internal static string? DetectRequestedModule(string prompt)
	{
		var searchablePrompt = Regex.Replace(
			prompt,
			@"(?i)\b(subject|teacher|chapter)\s+assignments?\b",
			string.Empty);
		return ModuleTerms.FirstOrDefault(module =>
			module.Value.Any(term => ContainsTerm(searchablePrompt, term))).Key;
	}

	private static void ValidateTokens(IEnumerable<string>? tokens, IEnumerable<string> availableTokens, string tokenType)
	{
		var requested = (tokens ?? []).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
		if (requested.Count > 250)
		{
			throw new BusinessException("Academic:Reports:TooManyFilterValues");
		}
		var available = availableTokens.ToHashSet(StringComparer.OrdinalIgnoreCase);
		if (requested.Any(token => !available.Contains(token)))
		{
			throw new BusinessException("Academic:Reports:AiInvalidCatalogToken")
				.WithData("TokenType", tokenType);
		}
	}

	private static string NormalizeText(string? value, string fallback, int maximumLength)
	{
		var normalized = Regex.Replace(value?.Trim() ?? string.Empty, @"\s+", " ");
		if (string.IsNullOrWhiteSpace(normalized))
		{
			normalized = fallback;
		}
		return normalized.Length <= maximumLength ? normalized : normalized[..maximumLength];
	}

	private static bool ContainsTerm(string prompt, string term) =>
		Regex.IsMatch(prompt, $@"(?i)(?<![a-z0-9]){Regex.Escape(term)}(?![a-z0-9])");

	private static BusinessException ModuleScopeMismatch(string allowedModule, string requestedModule) =>
		new BusinessException("Academic:Reports:AiModuleScopeMismatch")
			.WithData("AllowedModule", allowedModule)
			.WithData("RequestedModule", requestedModule);
}
