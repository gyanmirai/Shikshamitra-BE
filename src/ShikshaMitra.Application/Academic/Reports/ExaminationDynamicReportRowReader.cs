using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Academic.Examination.Results;
using ShikshaMitra.Students;
using ShikshaMitra.Students.StudentRecords;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Academic.Reports;

public class ExaminationDynamicReportRowReader(
	IAsyncQueryableExecuter asyncExecuter,
	IExamRepository examRepository,
	IRepository<ExamMark, Guid> examMarkRepository,
	IRepository<ExamResultHeader, Guid> resultHeaderRepository,
	IRepository<ExamResultDetail, Guid> resultDetailRepository,
	IStudentRepository studentRepository,
	StudentScopeAccessService studentScopeAccessService) : ITransientDependency
{
	public Task<List<Dictionary<string, string?>>> ReadAsync(
		AcademicDynamicReportSpecDto plan,
		AcademicAiReportCatalogScope scope,
		AcademicDynamicReportCatalogSnapshot snapshot,
		int maximumRows) => plan.Dataset switch
	{
		AcademicDynamicDatasets.ExaminationSchedule => ReadScheduleAsync(plan, snapshot, maximumRows),
		AcademicDynamicDatasets.ExaminationMarks => ReadMarksAsync(plan, scope, snapshot, maximumRows),
		AcademicDynamicDatasets.ExaminationResults => ReadResultsAsync(plan, scope, snapshot, maximumRows),
		_ => throw AcademicDynamicReportGuard.DatasetNotAllowed(plan.Dataset)
	};

	private async Task<List<Dictionary<string, string?>>> ReadScheduleAsync(
		AcademicDynamicReportSpecDto plan,
		AcademicDynamicReportCatalogSnapshot snapshot,
		int maximumRows)
	{
		var filters = ResolveScope(plan, snapshot, includeSubjects: true);
		if (!filters.HasRequiredScope(includeSubjects: true)) return [];

		var exams = (await examRepository.GetQueryableAsync())
			.Where(exam => filters.ClassIds.Contains(exam.StandardId));
		var records = await asyncExecuter.ToListAsync((
			from exam in exams
			from division in exam.ExamDivisions
			from subject in exam.ExamSubjects
			where filters.DivisionIds.Contains(division.DivisionId) && filters.SubjectIds.Contains(subject.SubjectId)
			select new ScheduleProjection
				{
					ExamName = exam.ExamName,
					StandardId = exam.StandardId,
					DivisionId = division.DivisionId,
					SubjectId = subject.SubjectId,
					ExamDate = subject.ExamDate,
					StartTime = subject.StartTime,
					EndTime = subject.EndTime,
					TotalMarks = subject.TotalMarks,
					PassingMarks = subject.PassingMarks,
					ExamStatus = (int)exam.Status,
					Notes = subject.Notes
				})
			.Take(maximumRows + 1));

		var names = Names(snapshot);
		return records.Select(item => Row(
			("examName", item.ExamName), ("class", names.Classes.GetValueOrDefault(item.StandardId)),
			("division", names.Divisions.GetValueOrDefault(item.DivisionId)),
			("subject", names.Subjects.GetValueOrDefault(item.SubjectId)),
			("examDate", Date(item.ExamDate)), ("startTime", Time(item.StartTime)),
			("endTime", Time(item.EndTime)), ("totalMarks", Number(item.TotalMarks)),
			("passingMarks", Number(item.PassingMarks)),
			("examStatus", EnumName<ExamStatus>(item.ExamStatus)), ("notes", item.Notes))).ToList();
	}

	private async Task<List<Dictionary<string, string?>>> ReadMarksAsync(
		AcademicDynamicReportSpecDto plan,
		AcademicAiReportCatalogScope scope,
		AcademicDynamicReportCatalogSnapshot snapshot,
		int maximumRows)
	{
		var filters = ResolveScope(plan, snapshot, includeSubjects: true);
		if (!filters.HasRequiredScope(includeSubjects: true)) return [];

		var exams = (await examRepository.GetQueryableAsync())
			.Where(exam => filters.ClassIds.Contains(exam.StandardId));
		var marks = (await examMarkRepository.GetQueryableAsync()).Where(mark =>
			filters.DivisionIds.Contains(mark.DivisionId) && filters.SubjectIds.Contains(mark.SubjectId));
		var examSubjects = await GetExamSubjectsAsync();
		var students = await BuildScopedStudentsAsync(scope, filters);

		var records = await asyncExecuter.ToListAsync((
			from mark in marks
			join exam in exams on mark.ExamId equals exam.Id
			join student in students on mark.StudentId equals student.Id
			join subject in examSubjects
				on new { mark.ExamId, mark.SubjectId } equals new { subject.ExamId, subject.SubjectId }
			select new MarkProjection
			{
				ExamName = exam.ExamName,
				StandardId = exam.StandardId,
				DivisionId = mark.DivisionId,
				SubjectId = mark.SubjectId,
				GrNumber = student.GrNumber,
				GrNo = student.GrNo,
				RollNumber = student.RollNo,
				FirstName = student.FirstName,
				MiddleName = student.MiddleName,
				LastName = student.LastName,
				AttendanceStatus = (int)mark.AttendanceStatus,
				MarksObtained = mark.MarksObtained,
				TotalMarks = subject.TotalMarks,
				PassingMarks = subject.PassingMarks,
				IsPublished = mark.IsPublished,
				Feedback = mark.Feedback
			}).Take(maximumRows + 1));

		var names = Names(snapshot);
		return records.Select(item =>
		{
			var percentage = item.MarksObtained.HasValue && item.TotalMarks is > 0
				? Math.Round(item.MarksObtained.Value * 100d / item.TotalMarks.Value, 2)
				: (double?)null;
			var attendance = (ExamAttendanceStatus)item.AttendanceStatus;
			var result = attendance != ExamAttendanceStatus.Present
				? attendance.ToString()
				: !item.MarksObtained.HasValue || !item.PassingMarks.HasValue
					? "Not evaluated"
					: item.MarksObtained.Value >= item.PassingMarks.Value ? "Pass" : "Fail";
			return Row(
				("examName", item.ExamName), ("class", names.Classes.GetValueOrDefault(item.StandardId)),
				("division", names.Divisions.GetValueOrDefault(item.DivisionId)),
				("subject", names.Subjects.GetValueOrDefault(item.SubjectId)),
				("grNumber", item.GrNumber ?? item.GrNo?.ToString(CultureInfo.InvariantCulture)),
				("rollNumber", item.RollNumber?.ToString(CultureInfo.InvariantCulture)),
				("studentName", FullName(item.FirstName, item.MiddleName, item.LastName)),
				("attendanceStatus", attendance.ToString()), ("marksObtained", Number(item.MarksObtained)),
				("totalMarks", Number(item.TotalMarks)), ("percentage", Number(percentage)),
				("result", result), ("isPublished", item.IsPublished ? "Yes" : "No"),
				("feedback", item.Feedback));
		}).ToList();
	}

	private async Task<List<Dictionary<string, string?>>> ReadResultsAsync(
		AcademicDynamicReportSpecDto plan,
		AcademicAiReportCatalogScope scope,
		AcademicDynamicReportCatalogSnapshot snapshot,
		int maximumRows)
	{
		var filters = ResolveScope(plan, snapshot, includeSubjects: false);
		if (!filters.HasRequiredScope(includeSubjects: false)) return [];

		var exams = (await examRepository.GetQueryableAsync())
			.Where(exam => filters.ClassIds.Contains(exam.StandardId));
		var headers = (await resultHeaderRepository.GetQueryableAsync())
			.Where(header => filters.DivisionIds.Contains(header.DivisionId));
		var details = (await resultDetailRepository.GetQueryableAsync())
			.Where(detail => filters.DivisionIds.Contains(detail.DivisionId));
		var students = await BuildScopedStudentsAsync(scope, filters);

		var records = await asyncExecuter.ToListAsync((
			from detail in details
			join header in headers on detail.ResultHeaderId equals header.Id
			join exam in exams on detail.ExamId equals exam.Id
			join student in students on detail.StudentId equals student.Id
			select new ResultProjection
			{
				ExamName = exam.ExamName,
				StandardId = exam.StandardId,
				DivisionId = detail.DivisionId,
				GrNumber = student.GrNumber,
				GrNo = student.GrNo,
				RollNumber = student.RollNo,
				FirstName = student.FirstName,
				MiddleName = student.MiddleName,
				LastName = student.LastName,
				TotalMaxMarks = detail.TotalMaxMarks,
				TotalObtainedMarks = detail.TotalObtainedMarks,
				Percentage = detail.Percentage,
				Grade = detail.Grade,
				ClassRank = detail.ClassRank,
				SectionRank = detail.SectionRank,
				ResultStatus = (int)detail.ResultStatus,
				PublicationStatus = (int)header.Status,
				GeneratedOn = header.GeneratedOn,
				PublishedOn = header.PublishedOn
			}).Take(maximumRows + 1));

		var names = Names(snapshot);
		return records.Select(item => Row(
			("examName", item.ExamName), ("class", names.Classes.GetValueOrDefault(item.StandardId)),
			("division", names.Divisions.GetValueOrDefault(item.DivisionId)),
			("grNumber", item.GrNumber ?? item.GrNo?.ToString(CultureInfo.InvariantCulture)),
			("rollNumber", item.RollNumber?.ToString(CultureInfo.InvariantCulture)),
			("studentName", FullName(item.FirstName, item.MiddleName, item.LastName)),
			("totalMaxMarks", Number(item.TotalMaxMarks)),
			("totalObtainedMarks", Number(item.TotalObtainedMarks)),
			("percentage", Number(item.Percentage)), ("grade", item.Grade),
			("classRank", Number(item.ClassRank)), ("sectionRank", Number(item.SectionRank)),
			("resultStatus", EnumName<ExamStudentResultStatus>(item.ResultStatus)),
			("publicationStatus", EnumName<ExamResultStatus>(item.PublicationStatus)),
			("generatedOn", Date(item.GeneratedOn)), ("publishedOn", Date(item.PublishedOn)))).ToList();
	}

	private async Task<IQueryable<Student>> BuildScopedStudentsAsync(
		AcademicAiReportCatalogScope scope,
		ReportScope filters)
	{
		var students = (await studentRepository.GetQueryableWithoutAutoIncludesAsync()).Where(student =>
			student.AcademicYearId == scope.AcademicYearId &&
			student.AcademicBoardId == scope.EducationBoardId &&
			student.AcademicMediumId == scope.EducationMediumId &&
			student.StandardId.HasValue && filters.ClassIds.Contains(student.StandardId.Value) &&
			student.DivisionId.HasValue && filters.DivisionIds.Contains(student.DivisionId.Value));
		return await studentScopeAccessService.ApplyCurrentUserScopeAsync(students);
	}

	private async Task<IQueryable<ExamSubject>> GetExamSubjectsAsync()
	{
		var exams = await examRepository.GetQueryableAsync();
		return exams.SelectMany(exam => exam.ExamSubjects);
	}

	private static ReportScope ResolveScope(
		AcademicDynamicReportSpecDto plan,
		AcademicDynamicReportCatalogSnapshot snapshot,
		bool includeSubjects)
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
			includeSubjects
				? snapshot.Subjects.Select(item => item.Id)
					.Where(id => selectedSubjectIds.Count == 0 || selectedSubjectIds.Contains(id)).ToList()
				: []);
	}

	private static ReportNames Names(AcademicDynamicReportCatalogSnapshot snapshot) => new(
		snapshot.Classes.ToDictionary(item => item.Id, item => item.Name),
		snapshot.Divisions.ToDictionary(item => item.Id, item => item.Name),
		snapshot.Subjects.ToDictionary(item => item.Id, item => item.Name));

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
	private static string? Time(DateTime? value) => value?.ToString("HH:mm", CultureInfo.InvariantCulture);
	private static string? Number(double? value) => value?.ToString("0.##", CultureInfo.InvariantCulture);
	private static string? Number(int? value) => value?.ToString(CultureInfo.InvariantCulture);
	private static string FullName(params string?[] parts) =>
		string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));

	private sealed record ReportScope(List<Guid> ClassIds, List<Guid> DivisionIds, List<Guid> SubjectIds)
	{
		public bool HasRequiredScope(bool includeSubjects) =>
			ClassIds.Count > 0 && DivisionIds.Count > 0 && (!includeSubjects || SubjectIds.Count > 0);
	}

	private sealed record ReportNames(
		Dictionary<Guid, string> Classes,
		Dictionary<Guid, string> Divisions,
		Dictionary<Guid, string> Subjects);

	private sealed class ScheduleProjection
	{
		public string ExamName { get; set; } = string.Empty;
		public Guid StandardId { get; set; }
		public Guid DivisionId { get; set; }
		public Guid SubjectId { get; set; }
		public DateTime? ExamDate { get; set; }
		public DateTime? StartTime { get; set; }
		public DateTime? EndTime { get; set; }
		public double? TotalMarks { get; set; }
		public double? PassingMarks { get; set; }
		public int ExamStatus { get; set; }
		public string? Notes { get; set; }
	}

	private class StudentProjection
	{
		public string? GrNumber { get; set; }
		public int? GrNo { get; set; }
		public int? RollNumber { get; set; }
		public string? FirstName { get; set; }
		public string? MiddleName { get; set; }
		public string? LastName { get; set; }
	}

	private sealed class MarkProjection : StudentProjection
	{
		public string ExamName { get; set; } = string.Empty;
		public Guid StandardId { get; set; }
		public Guid DivisionId { get; set; }
		public Guid SubjectId { get; set; }
		public int AttendanceStatus { get; set; }
		public double? MarksObtained { get; set; }
		public double? TotalMarks { get; set; }
		public double? PassingMarks { get; set; }
		public bool IsPublished { get; set; }
		public string? Feedback { get; set; }
	}

	private sealed class ResultProjection : StudentProjection
	{
		public string ExamName { get; set; } = string.Empty;
		public Guid StandardId { get; set; }
		public Guid DivisionId { get; set; }
		public double TotalMaxMarks { get; set; }
		public double TotalObtainedMarks { get; set; }
		public double Percentage { get; set; }
		public string Grade { get; set; } = string.Empty;
		public int? ClassRank { get; set; }
		public int? SectionRank { get; set; }
		public int ResultStatus { get; set; }
		public int PublicationStatus { get; set; }
		public DateTime? GeneratedOn { get; set; }
		public DateTime? PublishedOn { get; set; }
	}
}
