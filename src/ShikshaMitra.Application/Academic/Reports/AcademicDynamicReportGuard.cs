using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Academic.Reports;

public class AcademicDynamicReportGuard(AcademicDynamicReportCatalog catalog) : ITransientDependency
{
	private static readonly string[] KnownModules =
	[
		AcademicReportModules.Curriculum, AcademicReportModules.HomeworkAssignment,
		AcademicReportModules.Examination, AcademicReportModules.Timetable, AcademicReportModules.LearningManagement,
		AcademicReportModules.StudentRecords, AcademicReportModules.StudentAttendance,
		AcademicReportModules.HumanResources, AcademicReportModules.Finance,
		AcademicReportModules.Library, AcademicReportModules.Transport
	];

	private static readonly IReadOnlyDictionary<string, string[]> ModuleTerms =
		new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
		{
			[AcademicReportModules.HomeworkAssignment] =
				["homework", "home work", "student assignment", "student assignments", "assignment report", "assignments report", "assignment submission", "rubric"],
			[AcademicReportModules.Examination] =
				["exam", "examination", "student marks", "exam marks", "result report", "student results", "grade analysis"],
			[AcademicReportModules.Timetable] = ["timetable", "time table", "class schedule", "room utilization"],
			[AcademicReportModules.LearningManagement] = ["learning management", "lms report", "online course", "learning analytics"],
			[AcademicReportModules.StudentAttendance] = ["student attendance", "student absence", "absentee report", "attendance trend"],
			[AcademicReportModules.StudentRecords] = ["student record", "student profile", "student enrollment", "admission report"],
			[AcademicReportModules.HumanResources] = ["hr report", "employee report", "staff attendance", "payroll", "leave report"],
			[AcademicReportModules.Finance] = ["finance report", "fee collection", "fee report", "accounting report", "outstanding fees"],
			[AcademicReportModules.Library] = ["library report", "book circulation", "overdue books", "library fine"],
			[AcademicReportModules.Transport] = ["transport report", "vehicle report", "bus route", "transport fee"]
		};

	public void EnsureRequestAllowed(string prompt, string module)
	{
		module = NormalizeModule(module);
		if (catalog.GetDatasets(module).Count == 0)
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
		if (requestedModule is not null &&
			!string.Equals(requestedModule, module, StringComparison.OrdinalIgnoreCase))
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
		plan.Format = NormalizeFormat(plan.Format);
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
		ValidateFilterTypes(plan, dataset);
		ValidateTokens(plan.Filters.ClassTokens, snapshot.Classes.Select(item => item.Token), "class");
		ValidateTokens(plan.Filters.DivisionTokens, snapshot.Divisions.Select(item => item.Token), "division");
		ValidateTokens(plan.Filters.SubjectTokens, snapshot.Subjects.Select(item => item.Token), "subject");
		ValidateTokens(plan.Filters.ChapterTokens, snapshot.Chapters.Select(item => item.Token), "chapter");
		plan.Where ??= new AcademicDynamicReportWhereDto();
		plan.Where.Match = string.Equals(
			plan.Where.Match, AcademicDynamicMatchModes.Any, StringComparison.OrdinalIgnoreCase)
			? AcademicDynamicMatchModes.Any
			: AcademicDynamicMatchModes.All;
		plan.Where.Conditions ??= [];
		if (plan.Where.Conditions.Count > 8)
		{
			throw new BusinessException("Academic:Reports:AiTooManyConditions");
		}
		foreach (var condition in plan.Where.Conditions)
		{
			ValidateCondition(condition, dataset);
		}

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

	private static void ValidateCondition(
		AcademicDynamicReportConditionDto condition,
		AcademicDynamicDatasetDefinition dataset)
	{
		condition.Field = condition.Field?.Trim() ?? string.Empty;
		condition.Operator = condition.Operator?.Trim() ?? string.Empty;
		var field = dataset.Columns.FirstOrDefault(column =>
			string.Equals(column.Id, condition.Field, StringComparison.OrdinalIgnoreCase));
		var canonicalOperator = field == null
			? null
			: AllowedOperators(field.DataType).FirstOrDefault(value =>
				string.Equals(value, condition.Operator, StringComparison.OrdinalIgnoreCase));
		if (field == null || canonicalOperator == null)
		{
			throw new BusinessException("Academic:Reports:AiConditionNotAllowed")
				.WithData("Field", condition.Field)
				.WithData("Operator", condition.Operator);
		}

		condition.Field = field.Id;
		condition.Operator = canonicalOperator;
		condition.Values = (condition.Values ?? []).Select(value => value.Trim()).ToList();
		var validValueCount = condition.Operator switch
		{
			AcademicDynamicFilterOperators.IsEmpty or AcademicDynamicFilterOperators.IsNotEmpty => condition.Values.Count == 0,
			AcademicDynamicFilterOperators.Between => condition.Values.Count == 2,
			AcademicDynamicFilterOperators.In => condition.Values is { Count: >= 1 and <= 10 },
			_ => condition.Values.Count == 1
		};
		if (!validValueCount || condition.Values.Any(string.IsNullOrWhiteSpace))
		{
			throw new BusinessException("Academic:Reports:AiConditionValueInvalid");
		}
		if (field.DataType == AcademicDynamicFieldTypes.Number &&
			condition.Values.Any(value => !TryParseNumber(value, out _)))
		{
			throw new BusinessException("Academic:Reports:AiConditionValueInvalid");
		}
		if (field.DataType == AcademicDynamicFieldTypes.Date &&
			condition.Values.Any(value => !DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out _)))
		{
			throw new BusinessException("Academic:Reports:AiConditionValueInvalid");
		}
	}

	private static IReadOnlyList<string> AllowedOperators(string dataType) =>
		dataType switch
		{
			AcademicDynamicFieldTypes.Number or AcademicDynamicFieldTypes.Date =>
			[
				AcademicDynamicFilterOperators.Equal, AcademicDynamicFilterOperators.NotEquals,
				AcademicDynamicFilterOperators.In, AcademicDynamicFilterOperators.GreaterThan,
				AcademicDynamicFilterOperators.GreaterThanOrEqual, AcademicDynamicFilterOperators.LessThan,
				AcademicDynamicFilterOperators.LessThanOrEqual, AcademicDynamicFilterOperators.Between,
				AcademicDynamicFilterOperators.IsEmpty, AcademicDynamicFilterOperators.IsNotEmpty
			],
			_ =>
			[
				AcademicDynamicFilterOperators.Equal, AcademicDynamicFilterOperators.NotEquals,
				AcademicDynamicFilterOperators.Contains, AcademicDynamicFilterOperators.StartsWith,
				AcademicDynamicFilterOperators.In, AcademicDynamicFilterOperators.IsEmpty,
				AcademicDynamicFilterOperators.IsNotEmpty
			]
		};

	internal static bool TryParseNumber(string? value, out decimal number) =>
		decimal.TryParse(
			value?.Trim().TrimEnd('%'),
			NumberStyles.Number,
			CultureInfo.InvariantCulture,
			out number);

	private static string NormalizeFormat(string? format)
	{
		if (string.Equals(format, AcademicReportFormat.Excel, StringComparison.OrdinalIgnoreCase))
		{
			return AcademicReportFormat.Excel;
		}
		if (string.Equals(format, AcademicReportFormat.Csv, StringComparison.OrdinalIgnoreCase))
		{
			return AcademicReportFormat.Csv;
		}
		return AcademicReportFormat.Pdf;
	}

	public static string NormalizeModule(string? module)
	{
		if (string.IsNullOrWhiteSpace(module)) return AcademicReportModules.Curriculum;
		var trimmed = module.Trim();
		if (string.Equals(trimmed, AcademicReportModules.Homework, StringComparison.OrdinalIgnoreCase) ||
			string.Equals(trimmed, AcademicReportModules.Assignment, StringComparison.OrdinalIgnoreCase))
		{
			return AcademicReportModules.HomeworkAssignment;
		}
		return KnownModules.FirstOrDefault(known => string.Equals(known, trimmed, StringComparison.OrdinalIgnoreCase))
			?? trimmed;
	}

	private static void ValidateFilterTypes(
		AcademicDynamicReportSpecDto plan,
		AcademicDynamicDatasetDefinition dataset)
	{
		var requestedTypes = (plan.RequestedFilterTypes ?? [])
			.Where(type => !string.IsNullOrWhiteSpace(type))
			.Select(type => type.Trim())
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();
		var selectedTypes = new List<string>();
		if (plan.Filters.ClassTokens is { Count: > 0 }) selectedTypes.Add(AcademicDynamicFilterTypes.Class);
		if (plan.Filters.DivisionTokens is { Count: > 0 }) selectedTypes.Add(AcademicDynamicFilterTypes.Division);
		if (plan.Filters.SubjectTokens is { Count: > 0 }) selectedTypes.Add(AcademicDynamicFilterTypes.Subject);
		if (plan.Filters.ChapterTokens is { Count: > 0 }) selectedTypes.Add(AcademicDynamicFilterTypes.Chapter);

		var supported = dataset.SupportedFilterTypes.ToHashSet(StringComparer.OrdinalIgnoreCase);
		var unsupported = requestedTypes.Concat(selectedTypes).FirstOrDefault(type =>
			!AcademicDynamicFilterTypes.All.Contains(type, StringComparer.OrdinalIgnoreCase) ||
			!supported.Contains(type));
		if (unsupported is not null)
		{
			throw new BusinessException("Academic:Reports:AiFilterNotAllowed")
				.WithData("FilterType", unsupported)
				.WithData("Dataset", dataset.Id);
		}
		plan.RequestedFilterTypes = requestedTypes;
	}

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
