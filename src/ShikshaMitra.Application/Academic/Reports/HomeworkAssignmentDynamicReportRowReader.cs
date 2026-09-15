using ShikshaMitra.Academic.HomeworkAssignment;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Academic.Reports;

public class HomeworkAssignmentDynamicReportRowReader(
	IAsyncQueryableExecuter asyncExecuter,
	IRepository<HomeworkAssignmentEntity, Guid> homeworkRepository,
	IRepository<AssignmentEntity, Guid> assignmentRepository) : ITransientDependency
{
	public Task<List<Dictionary<string, string?>>> ReadAsync(
		AcademicDynamicReportSpecDto plan,
		AcademicAiReportCatalogScope scope,
		AcademicDynamicReportCatalogSnapshot snapshot,
		int maximumRows) => plan.Dataset switch
	{
		AcademicDynamicDatasets.HomeworkTasks => ReadHomeworkAsync(plan, scope, snapshot, maximumRows),
		AcademicDynamicDatasets.AssignmentTasks => ReadAssignmentsAsync(plan, scope, snapshot, maximumRows),
		_ => throw AcademicDynamicReportGuard.DatasetNotAllowed(plan.Dataset)
	};

	private async Task<List<Dictionary<string, string?>>> ReadHomeworkAsync(
		AcademicDynamicReportSpecDto plan,
		AcademicAiReportCatalogScope scope,
		AcademicDynamicReportCatalogSnapshot snapshot,
		int maximumRows)
	{
		var filters = ResolveScope(plan, snapshot);
		if (filters.ClassIds.Count == 0 || filters.DivisionIds.Count == 0 || filters.SubjectIds.Count == 0)
		{
			return [];
		}

		var query = (await homeworkRepository.GetQueryableAsync()).Where(item =>
			item.AcademicYearId == scope.AcademicYearId &&
			item.EducationBoardId == scope.EducationBoardId &&
			item.EducationMediumId == scope.EducationMediumId &&
			filters.ClassIds.Contains(item.StandardId) &&
			filters.SubjectIds.Contains(item.SubjectId));

		var scheduled = await asyncExecuter.ToListAsync(query.SelectMany(item =>
			item.DivisionSchedules
				.Where(schedule => filters.DivisionIds.Contains(schedule.DivisionId))
				.Select(schedule => new TaskProjection
				{
					StandardId = item.StandardId,
					DivisionId = schedule.DivisionId,
					SubjectId = item.SubjectId,
					Title = item.Title,
					Description = item.Description,
					Status = (int)item.Status,
					SubmissionType = (int)item.SubmissionType,
					PublishDate = schedule.PublishDate,
					DueDate = schedule.DueDate,
					IsPublished = schedule.IsPublished
				})).Take(maximumRows + 1));

		var legacy = await asyncExecuter.ToListAsync(query
			.Where(item => !item.DivisionSchedules.Any() && item.DivisionId.HasValue &&
				filters.DivisionIds.Contains(item.DivisionId.Value))
			.Select(item => new TaskProjection
			{
				StandardId = item.StandardId,
				DivisionId = item.DivisionId!.Value,
				SubjectId = item.SubjectId,
				Title = item.Title,
				Description = item.Description,
				Status = (int)item.Status,
				SubmissionType = (int)item.SubmissionType,
				DueDate = item.DueDate,
				IsPublished = item.IsPublished
			}).Take(maximumRows + 1));

		return scheduled.Concat(legacy)
			.Select(item => BuildCommonRow(item, snapshot,
				EnumName<HomeworkStatus>(item.Status)))
			.Take(maximumRows + 1)
			.ToList();
	}

	private async Task<List<Dictionary<string, string?>>> ReadAssignmentsAsync(
		AcademicDynamicReportSpecDto plan,
		AcademicAiReportCatalogScope scope,
		AcademicDynamicReportCatalogSnapshot snapshot,
		int maximumRows)
	{
		var filters = ResolveScope(plan, snapshot);
		if (filters.ClassIds.Count == 0 || filters.DivisionIds.Count == 0 || filters.SubjectIds.Count == 0)
		{
			return [];
		}

		var query = (await assignmentRepository.GetQueryableAsync()).Where(item =>
			item.AcademicYearId == scope.AcademicYearId &&
			item.EducationBoardId == scope.EducationBoardId &&
			item.EducationMediumId == scope.EducationMediumId &&
			filters.ClassIds.Contains(item.StandardId) &&
			filters.SubjectIds.Contains(item.SubjectId));

		var scheduled = await asyncExecuter.ToListAsync(query.SelectMany(item =>
			item.DivisionSchedules
				.Where(schedule => filters.DivisionIds.Contains(schedule.DivisionId))
				.Select(schedule => new TaskProjection
				{
					StandardId = item.StandardId,
					DivisionId = schedule.DivisionId,
					SubjectId = item.SubjectId,
					Title = item.Title,
					Description = item.Description,
					Status = (int)item.Status,
					SubmissionType = (int)item.SubmissionType,
					PublishDate = schedule.PublishDate,
					DueDate = schedule.DueDate,
					IsPublished = schedule.IsPublished,
					TotalMarks = item.TotalMarks,
					EvaluationRequired = item.EvaluationRequired,
					EvaluationMethod = (int?)item.EvaluationMethod
				})).Take(maximumRows + 1));

		var legacy = await asyncExecuter.ToListAsync(query
			.Where(item => !item.DivisionSchedules.Any() && item.DivisionId != Guid.Empty &&
				filters.DivisionIds.Contains(item.DivisionId))
			.Select(item => new TaskProjection
			{
				StandardId = item.StandardId,
				DivisionId = item.DivisionId,
				SubjectId = item.SubjectId,
				Title = item.Title,
				Description = item.Description,
				Status = (int)item.Status,
				SubmissionType = (int)item.SubmissionType,
				TotalMarks = item.TotalMarks,
				EvaluationRequired = item.EvaluationRequired,
				EvaluationMethod = (int?)item.EvaluationMethod
			}).Take(maximumRows + 1));

		return scheduled.Concat(legacy).Select(item =>
		{
			var row = BuildCommonRow(item, snapshot, EnumName<AssignmentStatus>(item.Status));
			row["totalMarks"] = item.TotalMarks?.ToString(CultureInfo.InvariantCulture);
			row["evaluationRequired"] = YesNo(item.EvaluationRequired);
			row["evaluationMethod"] = item.EvaluationMethod.HasValue
				? EnumName<EvaluationMethod>(item.EvaluationMethod.Value)
				: null;
			return row;
		}).Take(maximumRows + 1).ToList();
	}

	private static ReportScope ResolveScope(
		AcademicDynamicReportSpecDto plan,
		AcademicDynamicReportCatalogSnapshot snapshot)
	{
		var selectedClassIds = Resolve(plan.Filters.ClassTokens, snapshot.Classes, item => item.Token, item => item.Id)
			.Concat(Resolve(plan.Filters.DivisionTokens, snapshot.Divisions, item => item.Token, item => item.StandardId))
			.Distinct().ToHashSet();
		var selectedDivisionIds = Resolve(
			plan.Filters.DivisionTokens, snapshot.Divisions, item => item.Token, item => item.Id).ToHashSet();
		var selectedSubjectIds = Resolve(
			plan.Filters.SubjectTokens, snapshot.Subjects, item => item.Token, item => item.Id).ToHashSet();

		return new ReportScope(
			snapshot.Classes.Select(item => item.Id)
				.Where(id => selectedClassIds.Count == 0 || selectedClassIds.Contains(id)).ToList(),
			snapshot.Divisions.Select(item => item.Id)
				.Where(id => selectedDivisionIds.Count == 0 || selectedDivisionIds.Contains(id)).ToList(),
			snapshot.Subjects.Select(item => item.Id)
				.Where(id => selectedSubjectIds.Count == 0 || selectedSubjectIds.Contains(id)).ToList());
	}

	private static Dictionary<string, string?> BuildCommonRow(
		TaskProjection item,
		AcademicDynamicReportCatalogSnapshot snapshot,
		string status)
	{
		var classNames = snapshot.Classes.ToDictionary(value => value.Id, value => value.Name);
		var divisionNames = snapshot.Divisions.ToDictionary(value => value.Id, value => value.Name);
		var subjectNames = snapshot.Subjects.ToDictionary(value => value.Id, value => value.Name);
		return Row(
			("class", classNames.GetValueOrDefault(item.StandardId)),
			("division", divisionNames.GetValueOrDefault(item.DivisionId)),
			("subject", subjectNames.GetValueOrDefault(item.SubjectId)),
			("title", item.Title), ("status", status),
			("submissionType", EnumName<SubmissionType>(item.SubmissionType)),
			("publishDate", Date(item.PublishDate)), ("dueDate", Date(item.DueDate)),
			("isPublished", item.IsPublished ? "Yes" : "No"), ("description", item.Description));
	}

	private static IEnumerable<TResult> Resolve<T, TResult>(IEnumerable<string> tokens, IEnumerable<T> catalog,
		Func<T, string> tokenSelector, Func<T, TResult> valueSelector)
	{
		var tokenSet = tokens.ToHashSet(StringComparer.OrdinalIgnoreCase);
		return catalog.Where(item => tokenSet.Contains(tokenSelector(item))).Select(valueSelector);
	}

	private static Dictionary<string, string?> Row(params (string Key, string? Value)[] values) =>
		values.ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
	private static string EnumName<TEnum>(int value) where TEnum : struct, Enum =>
		Enum.GetName(typeof(TEnum), value) ?? value.ToString(CultureInfo.InvariantCulture);
	private static string? Date(DateTime? value) => value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
	private static string? YesNo(bool? value) => value.HasValue ? value.Value ? "Yes" : "No" : null;

	private sealed record ReportScope(List<Guid> ClassIds, List<Guid> DivisionIds, List<Guid> SubjectIds);

	private sealed class TaskProjection
	{
		public Guid StandardId { get; set; }
		public Guid DivisionId { get; set; }
		public Guid SubjectId { get; set; }
		public string Title { get; set; } = string.Empty;
		public string Description { get; set; } = string.Empty;
		public int Status { get; set; }
		public int SubmissionType { get; set; }
		public DateTime? PublishDate { get; set; }
		public DateTime? DueDate { get; set; }
		public bool IsPublished { get; set; }
		public decimal? TotalMarks { get; set; }
		public bool? EvaluationRequired { get; set; }
		public int? EvaluationMethod { get; set; }
	}
}
