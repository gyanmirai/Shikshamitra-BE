using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.HR.Employees;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Academic.Reports;

public class AcademicDynamicReportQueryService(
	IAsyncQueryableExecuter asyncExecuter,
	IRepository<AssignedSubject, Guid> assignedSubjectRepository,
	IEmployeeRepository employeeRepository,
	TimetableDynamicReportRowReader timetableRowReader,
	StudentRecordsDynamicReportRowReader studentRecordsRowReader,
	StudentAttendanceDynamicReportRowReader attendanceRowReader,
	HomeworkAssignmentDynamicReportRowReader homeworkAssignmentRowReader,
	ExaminationDynamicReportRowReader examinationRowReader,
	AcademicDynamicReportCatalog catalogService,
	AcademicDynamicReportGuard guard) : ITransientDependency
{
	private const int MaximumRows = 5000;

	public async Task<AcademicReportPreviewDto> GenerateAsync(
		AcademicDynamicReportSpecDto requestedPlan,
		AcademicAiReportCatalogScope scope)
	{
		var snapshot = await catalogService.BuildSnapshotAsync(scope);
		var plan = guard.ValidatePlan(requestedPlan, requestedPlan.Module, snapshot);
		var dataset = catalogService.GetDataset(plan.Dataset);
		var rows = plan.Module switch
		{
			AcademicReportModules.Timetable =>
				await timetableRowReader.ReadAsync(plan, scope, snapshot, MaximumRows),
			AcademicReportModules.StudentRecords =>
				await studentRecordsRowReader.ReadAsync(plan, scope, snapshot, MaximumRows),
			AcademicReportModules.StudentAttendance =>
				await attendanceRowReader.ReadAsync(plan, scope, snapshot, MaximumRows),
			AcademicReportModules.HomeworkAssignment =>
				await homeworkAssignmentRowReader.ReadAsync(plan, scope, snapshot, MaximumRows),
			AcademicReportModules.Examination =>
				await examinationRowReader.ReadAsync(plan, scope, snapshot, MaximumRows),
			_ => await LoadCurriculumRowsAsync(plan, scope, snapshot)
		};
		if (rows.Count > MaximumRows)
		{
			throw ResultTooLarge();
		}
		rows = ApplyWhere(rows, plan.Where, dataset);
		if (rows.Count > MaximumRows)
		{
			throw ResultTooLarge();
		}

		rows = ApplySort(rows, plan.Sort, dataset);
		var selectedColumns = dataset.Columns
			.Where(column => plan.Columns.Contains(column.Id, StringComparer.OrdinalIgnoreCase))
			.Select(column => new AcademicReportColumnDto { Id = column.Id, Label = column.Label })
			.ToList();
		var projectedRows = rows.Select(row => selectedColumns.ToDictionary(
			column => column.Id,
			column => row.GetValueOrDefault(column.Id),
			StringComparer.OrdinalIgnoreCase)).ToList();

		return new AcademicReportPreviewDto
		{
			ReportType = plan.Title,
			GeneratedAtUtc = DateTime.UtcNow,
			FilterSummary = BuildFilterSummary(plan, snapshot),
			TotalCount = projectedRows.Count,
			Page = 1,
			PageSize = projectedRows.Count,
			Columns = selectedColumns,
			Rows = projectedRows
		};
	}

	private async Task<List<Dictionary<string, string?>>> LoadCurriculumRowsAsync(
		AcademicDynamicReportSpecDto plan,
		AcademicAiReportCatalogScope scope,
		AcademicDynamicReportCatalogSnapshot snapshot)
	{
		var assignments = await LoadAssignmentsAsync(plan.Filters, scope, snapshot);
		if (assignments.Count > MaximumRows)
		{
			throw ResultTooLarge();
		}
		var teacherIds = assignments.SelectMany(subject => subject.Teachers).Select(teacher => teacher.TeacherId)
			.Distinct().ToList();
		var employees = teacherIds.Count == 0
			? []
			: await employeeRepository.GetListAsync(employee => teacherIds.Contains(employee.Id));
		var employeeMap = employees.ToDictionary(employee => employee.Id);

		return plan.Dataset switch
		{
			AcademicDynamicDatasets.CurriculumSubjects => BuildSubjectRows(assignments, employeeMap),
			AcademicDynamicDatasets.CurriculumChapters => BuildChapterRows(assignments, plan.Filters, snapshot),
			AcademicDynamicDatasets.CurriculumTeachers => BuildTeacherRows(assignments, employeeMap),
			_ => throw AcademicDynamicReportGuard.DatasetNotAllowed(plan.Dataset)
		};
	}

	private async Task<List<AssignedSubject>> LoadAssignmentsAsync(
		AcademicDynamicReportFilterDto filters,
		AcademicAiReportCatalogScope scope,
		AcademicDynamicReportCatalogSnapshot snapshot)
	{
		var query = await assignedSubjectRepository.WithDetailsAsync(
			subject => subject.Subject,
			subject => subject.Standard,
			subject => subject.Chapters,
			subject => subject.Teachers);
		query = query.Where(subject =>
			subject.Standard.EducationMediumStage.EducationMediumId == scope.EducationMediumId);
		if (scope.ClassIds is { Count: > 0 })
		{
			query = query.Where(subject => scope.ClassIds.Contains(subject.StandardId));
		}

		var classIds = Resolve(filters.ClassTokens, snapshot.Classes, item => item.Token, item => item.Id)
			.Concat(Resolve(filters.DivisionTokens, snapshot.Divisions, item => item.Token, item => item.StandardId))
			.Concat(Resolve(filters.ChapterTokens, snapshot.Chapters, item => item.Token, item => item.StandardId))
			.Distinct().ToList();
		var subjectIds = Resolve(filters.SubjectTokens, snapshot.Subjects, item => item.Token, item => item.Id)
			.Concat(Resolve(filters.ChapterTokens, snapshot.Chapters, item => item.Token, item => item.SubjectId))
			.Distinct().ToList();
		var chapterIds = Resolve(filters.ChapterTokens, snapshot.Chapters, item => item.Token, item => item.Id).ToList();

		if (classIds.Count > 0) query = query.Where(subject => classIds.Contains(subject.StandardId));
		if (subjectIds.Count > 0) query = query.Where(subject => subjectIds.Contains(subject.SubjectId));
		if (chapterIds.Count > 0) query = query.Where(subject => subject.Chapters.Any(chapter => chapterIds.Contains(chapter.Id)));

		return await asyncExecuter.ToListAsync(query.Take(MaximumRows + 1));
	}

	private static List<Dictionary<string, string?>> BuildSubjectRows(
		IEnumerable<AssignedSubject> assignments,
		IReadOnlyDictionary<Guid, Employee> employees) =>
		assignments.Select(subject =>
		{
			var primary = subject.Teachers.FirstOrDefault(teacher => teacher.TeacherType == SubjectTeacherType.Primary);
			var secondaryNames = subject.Teachers.Where(teacher => teacher.TeacherType == SubjectTeacherType.Secondary)
				.Select(teacher => EmployeeName(employees.GetValueOrDefault(teacher.TeacherId)))
				.Where(name => !string.IsNullOrWhiteSpace(name)).OrderBy(name => name).ToList();
			var averageProgress = subject.Chapters.Count == 0 ? 0 : Math.Round(subject.Chapters.Average(chapter => chapter.ProgressPercentage), 2);
			return Row(
				("class", subject.Standard?.Name), ("subject", subject.Subject?.Name), ("subjectCode", subject.SubjectCode),
				("subjectType", subject.SubjectType.ToString()), ("status", subject.Status.ToString()),
				("weeklyPeriods", subject.WeeklyPeriods.ToString(CultureInfo.InvariantCulture)),
				("difficultyLevel", subject.DifficultyLevel.ToString()), ("subjectGroup", subject.SubjectGroup),
				("primaryTeacher", primary == null ? null : EmployeeName(employees.GetValueOrDefault(primary.TeacherId))),
				("secondaryTeachers", string.Join(", ", secondaryNames)),
				("chapterCount", subject.Chapters.Count.ToString(CultureInfo.InvariantCulture)),
				("averageProgress", $"{averageProgress:0.##}%"),
				("setupHealthScore", subject.SetupHealthScore.ToString(CultureInfo.InvariantCulture)),
				("learningGoals", subject.LearningGoals), ("prerequisites", subject.Prerequisites),
				("description", subject.Description));
		}).ToList();

	private static List<Dictionary<string, string?>> BuildChapterRows(
		IEnumerable<AssignedSubject> assignments,
		AcademicDynamicReportFilterDto filters,
		AcademicDynamicReportCatalogSnapshot snapshot)
	{
		var selectedChapterIds = Resolve(filters.ChapterTokens, snapshot.Chapters, item => item.Token, item => item.Id).ToHashSet();
		return assignments.SelectMany(subject => subject.Chapters
			.Where(chapter => selectedChapterIds.Count == 0 || selectedChapterIds.Contains(chapter.Id))
			.Select(chapter => Row(
				("class", subject.Standard?.Name), ("subject", subject.Subject?.Name),
				("chapterNumber", chapter.ChapterNumber.ToString(CultureInfo.InvariantCulture)),
				("chapterName", chapter.ChapterName), ("unitName", chapter.UnitName), ("termName", chapter.TermName),
				("topic", chapter.Topic), ("plannedStart", chapter.ChapterStartTime.ToString("dd MMM yyyy")),
				("plannedEnd", chapter.ChapterEndTime.ToString("dd MMM yyyy")),
				("estimatedTeachingDays", FormatNumber(chapter.EstimatedTeachingDays)),
				("estimatedPeriods", FormatNumber(chapter.EstimatedPeriods)), ("revisionDays", FormatNumber(chapter.RevisionDays)),
				("progressPercentage", $"{chapter.ProgressPercentage}%"), ("coverageStatus", chapter.CoverageStatus),
				("publishStatus", chapter.PublishStatus), ("reviewStatus", chapter.ReviewStatus),
				("healthScore", chapter.HealthScore.ToString(CultureInfo.InvariantCulture)),
				("marksWeightage", FormatNumber(chapter.MarksWeightage)), ("difficultyLevel", chapter.DifficultyLevel),
				("bloomLevel", chapter.BloomLevel), ("description", chapter.Description)))).ToList();
	}

	private static List<Dictionary<string, string?>> BuildTeacherRows(
		IEnumerable<AssignedSubject> assignments,
		IReadOnlyDictionary<Guid, Employee> employees) =>
		assignments.SelectMany(subject => subject.Teachers.Select(teacher =>
		{
			var employee = employees.GetValueOrDefault(teacher.TeacherId);
			return Row(
				("class", subject.Standard?.Name), ("subject", subject.Subject?.Name), ("subjectCode", subject.SubjectCode),
				("teacherName", EmployeeName(employee)), ("teacherCode", employee?.EmployeeCode),
				("teacherType", teacher.TeacherType.ToString()),
				("weeklyPeriods", subject.WeeklyPeriods.ToString(CultureInfo.InvariantCulture)),
				("subjectStatus", subject.Status.ToString()));
		})).ToList();

	private static List<Dictionary<string, string?>> ApplySort(
		List<Dictionary<string, string?>> rows,
		IReadOnlyList<AcademicDynamicReportSortDto> sort,
		AcademicDynamicDatasetDefinition dataset)
	{
		if (sort.Count == 0) return rows;
		var fields = dataset.Columns.ToDictionary(column => column.Id, StringComparer.OrdinalIgnoreCase);
		IOrderedEnumerable<Dictionary<string, string?>>? ordered = null;
		foreach (var item in sort)
		{
			Func<Dictionary<string, string?>, IComparable?> selector = row =>
				SortValue(row.GetValueOrDefault(item.Field), fields[item.Field].DataType);
			var descending = string.Equals(item.Direction, AcademicDynamicSortDirections.Descending, StringComparison.OrdinalIgnoreCase);
			ordered = ordered == null
				? descending ? rows.OrderByDescending(selector, DynamicValueComparer) : rows.OrderBy(selector, DynamicValueComparer)
				: descending ? ordered.ThenByDescending(selector, DynamicValueComparer) : ordered.ThenBy(selector, DynamicValueComparer);
		}
		return ordered?.ToList() ?? rows;
	}

	private static readonly IComparer<IComparable?> DynamicValueComparer =
		Comparer<IComparable?>.Create((left, right) =>
		{
			if (ReferenceEquals(left, right)) return 0;
			if (left == null) return -1;
			if (right == null) return 1;
			return left is string leftText && right is string rightText
				? StringComparer.OrdinalIgnoreCase.Compare(leftText, rightText)
				: left.CompareTo(right);
		});

	private static IComparable? SortValue(string? value, string dataType)
	{
		if (string.IsNullOrWhiteSpace(value)) return null;
		if (dataType == AcademicDynamicFieldTypes.Number &&
			AcademicDynamicReportGuard.TryParseNumber(value, out var number)) return number;
		if (dataType == AcademicDynamicFieldTypes.Date &&
			DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var date)) return date;
		return value;
	}

	private static List<Dictionary<string, string?>> ApplyWhere(
		List<Dictionary<string, string?>> rows,
		AcademicDynamicReportWhereDto where,
		AcademicDynamicDatasetDefinition dataset)
	{
		if (where.Conditions.Count == 0) return rows;
		var fields = dataset.Columns.ToDictionary(column => column.Id, StringComparer.OrdinalIgnoreCase);
		var matchAny = string.Equals(where.Match, AcademicDynamicMatchModes.Any, StringComparison.OrdinalIgnoreCase);
		return rows.Where(row => matchAny
			? where.Conditions.Any(condition => MatchesCondition(row, condition, fields[condition.Field]))
			: where.Conditions.All(condition => MatchesCondition(row, condition, fields[condition.Field])))
			.ToList();
	}

	internal static bool MatchesCondition(
		IReadOnlyDictionary<string, string?> row,
		AcademicDynamicReportConditionDto condition,
		AcademicDynamicColumnDefinition field)
	{
		var value = row.GetValueOrDefault(condition.Field)?.Trim();
		if (condition.Operator == AcademicDynamicFilterOperators.IsEmpty) return string.IsNullOrWhiteSpace(value);
		if (condition.Operator == AcademicDynamicFilterOperators.IsNotEmpty) return !string.IsNullOrWhiteSpace(value);
		if (string.IsNullOrWhiteSpace(value)) return false;

		if (field.DataType == AcademicDynamicFieldTypes.Number &&
			AcademicDynamicReportGuard.TryParseNumber(value, out var number))
		{
			var expected = condition.Values
				.Select(item => AcademicDynamicReportGuard.TryParseNumber(item, out var parsed) ? parsed : decimal.MinValue)
				.ToList();
			return Compare(number, expected, condition.Operator);
		}
		if (field.DataType == AcademicDynamicFieldTypes.Date &&
			DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var date))
		{
			var expected = condition.Values
				.Select(item => DateTime.Parse(item, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal))
				.ToList();
			return Compare(date, expected, condition.Operator);
		}

		return condition.Operator switch
		{
			AcademicDynamicFilterOperators.Equal => EqualsText(value, condition.Values[0]),
			AcademicDynamicFilterOperators.NotEquals => !EqualsText(value, condition.Values[0]),
			AcademicDynamicFilterOperators.Contains => value.Contains(condition.Values[0], StringComparison.OrdinalIgnoreCase),
			AcademicDynamicFilterOperators.StartsWith => value.StartsWith(condition.Values[0], StringComparison.OrdinalIgnoreCase),
			AcademicDynamicFilterOperators.In => condition.Values.Any(item => EqualsText(value, item)),
			_ => false
		};
	}

	private static bool Compare<T>(T value, IReadOnlyList<T> expected, string comparison)
		where T : IComparable<T> => comparison switch
	{
		AcademicDynamicFilterOperators.Equal => value.CompareTo(expected[0]) == 0,
		AcademicDynamicFilterOperators.NotEquals => value.CompareTo(expected[0]) != 0,
		AcademicDynamicFilterOperators.In => expected.Any(item => value.CompareTo(item) == 0),
		AcademicDynamicFilterOperators.GreaterThan => value.CompareTo(expected[0]) > 0,
		AcademicDynamicFilterOperators.GreaterThanOrEqual => value.CompareTo(expected[0]) >= 0,
		AcademicDynamicFilterOperators.LessThan => value.CompareTo(expected[0]) < 0,
		AcademicDynamicFilterOperators.LessThanOrEqual => value.CompareTo(expected[0]) <= 0,
		AcademicDynamicFilterOperators.Between => value.CompareTo(expected[0]) >= 0 && value.CompareTo(expected[1]) <= 0,
		_ => false
	};

	private static bool EqualsText(string left, string right) =>
		string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

	private static string BuildFilterSummary(AcademicDynamicReportSpecDto plan, AcademicDynamicReportCatalogSnapshot snapshot)
	{
		var values = new List<string> { $"Dataset: {plan.Dataset}" };
		AddFilter(values, "Classes", plan.Filters.ClassTokens, snapshot.Classes, item => item.Token, item => item.Name);
		AddFilter(values, "Divisions", plan.Filters.DivisionTokens, snapshot.Divisions, item => item.Token, item => item.Name);
		AddFilter(values, "Subjects", plan.Filters.SubjectTokens, snapshot.Subjects, item => item.Token, item => item.Name);
		AddFilter(values, "Chapters", plan.Filters.ChapterTokens, snapshot.Chapters, item => item.Token, item => item.Name);
		if (plan.Where.Conditions.Count > 0)
		{
			values.Add($"Conditions ({plan.Where.Match}): {string.Join("; ", plan.Where.Conditions.Select(condition =>
				$"{condition.Field} {condition.Operator} {string.Join(", ", condition.Values)}"))}");
		}
		return string.Join(" | ", values);
	}

	private static void AddFilter<T>(
		ICollection<string> summary,
		string label,
		IEnumerable<string> tokens,
		IEnumerable<T> catalog,
		Func<T, string> tokenSelector,
		Func<T, string> nameSelector)
	{
		var tokenSet = tokens.ToHashSet(StringComparer.OrdinalIgnoreCase);
		if (tokenSet.Count == 0) return;
		var names = catalog.Where(item => tokenSet.Contains(tokenSelector(item))).Select(nameSelector);
		summary.Add($"{label}: {string.Join(", ", names)}");
	}

	private static IEnumerable<TResult> Resolve<T, TResult>(
		IEnumerable<string> tokens,
		IEnumerable<T> catalog,
		Func<T, string> tokenSelector,
		Func<T, TResult> valueSelector)
	{
		var tokenSet = tokens.ToHashSet(StringComparer.OrdinalIgnoreCase);
		return catalog.Where(item => tokenSet.Contains(tokenSelector(item))).Select(valueSelector);
	}

	private static Dictionary<string, string?> Row(params (string Key, string? Value)[] values) =>
		values.ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);

	private static string? EmployeeName(Employee? employee) =>
		employee == null || string.IsNullOrWhiteSpace(employee.EmployeeName) ? null : employee.EmployeeName;

	private static string? FormatNumber(int? value) => value?.ToString(CultureInfo.InvariantCulture);

	private static BusinessException ResultTooLarge() =>
		new BusinessException("Academic:Reports:AiResultTooLarge")
			.WithData("MaximumRows", MaximumRows);
}
