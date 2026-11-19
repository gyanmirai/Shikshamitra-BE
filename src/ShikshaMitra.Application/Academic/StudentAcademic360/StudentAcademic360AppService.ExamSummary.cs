using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Academic.Examination.Results;
using ShikshaMitra.Academic.Examination.Results.Dtos;
using ShikshaMitra.Students;

namespace ShikshaMitra.Academic.StudentAcademic360;

public partial class StudentAcademic360AppService
{
	private async Task<IQueryable<Exam>> GetVisibleExamQueryAsync(Student student)
	{
		var headers = (await resultHeaderRepository.GetQueryableAsync()).Where(header =>
			header.DivisionId == student.DivisionId &&
			(header.Status == ExamResultStatus.Published || header.Status == ExamResultStatus.Frozen));
		var marks = (await examMarkRepository.GetQueryableAsync()).Where(mark =>
			mark.StudentId == student.Id && mark.DivisionId == student.DivisionId && mark.IsPublished);
		return (await examRepository.GetQueryableAsync()).Where(exam => exam.StandardId == student.StandardId &&
			exam.ExamDivisions.Any(division => division.DivisionId == student.DivisionId) &&
			(exam.Status == ExamStatus.Published || exam.Status == ExamStatus.Completed ||
			 headers.Any(header => header.ExamId == exam.Id) || marks.Any(mark => mark.ExamId == exam.Id)));
	}

	private async Task<List<StudentAcademic360ExamResultDto>> LoadExamSummaryAsync(Student student)
	{
		if (!student.StandardId.HasValue || !student.DivisionId.HasValue) return [];
		var visible = await GetVisibleExamQueryAsync(student);
		var facts = await AsyncExecuter.ToListAsync(visible.OrderByDescending(exam => exam.CreationTime).Select(exam => new
		{
			exam.Id, exam.ExamName, exam.Status,
			TotalMarks = exam.ExamSubjects.Sum(subject => subject.TotalMarks ?? 0),
			ExamDate = exam.ExamSubjects.Min(subject => subject.ExamDate),
			StartTime = exam.ExamSubjects.Min(subject => subject.StartTime),
			EndTime = exam.ExamSubjects.Max(subject => subject.EndTime)
		}));
		var marks = (await examMarkRepository.GetQueryableAsync()).Where(mark =>
			mark.StudentId == student.Id && mark.DivisionId == student.DivisionId && visible.Any(exam => exam.Id == mark.ExamId));
		var headers = await AsyncExecuter.ToListAsync((await resultHeaderRepository.GetQueryableAsync()).Where(header =>
			header.DivisionId == student.DivisionId && visible.Any(exam => exam.Id == header.ExamId) &&
			(header.Status == ExamResultStatus.Published || header.Status == ExamResultStatus.Frozen))
			.Select(header => new { header.Id, header.ExamId, header.Status, header.PublishedOn }));
		var details = await AsyncExecuter.ToListAsync((await resultDetailRepository.GetQueryableAsync()).Where(detail =>
			detail.StudentId == student.Id && detail.DivisionId == student.DivisionId && visible.Any(exam => exam.Id == detail.ExamId))
			.Select(detail => new { detail.ResultHeaderId, detail.TotalMaxMarks, detail.TotalObtainedMarks, detail.Percentage, detail.Grade, detail.ClassRank }));
		var markTotals = await AsyncExecuter.ToListAsync(marks.GroupBy(mark => mark.ExamId).Select(group => new
		{
			ExamId = group.Key, Obtained = group.Sum(mark => mark.IsAbsent ? 0 : mark.MarksObtained ?? 0)
		}));
		var names = await LoadSubjectNamesAsync(await AsyncExecuter.ToListAsync(visible.SelectMany(exam => exam.ExamSubjects.Select(subject => subject.SubjectId)).Distinct()));
		var subjectRows = await AsyncExecuter.ToListAsync(
			from exam in visible
			from subject in exam.ExamSubjects
			join mark in marks on new { ExamId = exam.Id, subject.SubjectId } equals new { mark.ExamId, mark.SubjectId } into subjectMarks
			from mark in subjectMarks.DefaultIfEmpty()
			select new
			{
				ExamId = exam.Id, subject.SubjectId, subject.TotalMarks,
				Mark = mark == null || mark.IsAbsent ? (double?)null : mark.MarksObtained ?? 0,
				IsAbsent = mark == null ? (bool?)null : mark.IsAbsent
			});
		var totalsByExam = markTotals.ToDictionary(row => row.ExamId, row => row.Obtained);
		var headersByExam = headers.ToLookup(header => header.ExamId);
		var detailsByHeader = details.ToLookup(detail => detail.ResultHeaderId);
		var subjectsByExam = subjectRows.ToLookup(row => row.ExamId);
		return facts.Select(exam =>
		{
			var header = headersByExam[exam.Id].FirstOrDefault();
			var detail = header == null ? null : detailsByHeader[header.Id].FirstOrDefault();
			var total = detail?.TotalMaxMarks ?? exam.TotalMarks;
			var obtained = detail?.TotalObtainedMarks ?? totalsByExam.GetValueOrDefault(exam.Id);
			var status = ResolveExamStatus(exam.Status, exam.ExamDate, exam.StartTime, exam.EndTime);
			var grading = ResolveGradingStatus(status, header?.Status);
			return new StudentAcademic360ExamResultDto
			{
				ExamId = exam.Id, ExamName = exam.ExamName, ExamDate = exam.ExamDate,
				StartDate = exam.StartTime, EndDate = exam.EndTime, ExamStatus = status,
				GradingStatus = grading, ResultStatus = grading, ResultAvailable = header != null,
				ResultPublishedDate = header?.PublishedOn, Rank = detail?.ClassRank,
				TotalMarks = total, ObtainedMarks = obtained, Grade = detail?.Grade ?? string.Empty,
				Percentage = detail?.Percentage ?? (total <= 0 ? 0 : Math.Round(obtained * 100d / total, 2)),
				Subjects = subjectsByExam[exam.Id].Select(subject => new StudentSubjectMarkDto
				{
					SubjectName = names.GetValueOrDefault(subject.SubjectId, "Subject"),
					MaxMarks = (subject.TotalMarks ?? 0).ToString("F2"),
					MarksObtained = subject.IsAbsent == true ? "Ab" : subject.Mark?.ToString("F2") ?? string.Empty
				}).ToList()
			};
		}).ToList();
	}

	private static bool IsExamGradingCompleted(StudentAcademic360ExamResultDto exam) =>
		exam.ResultAvailable || exam.GradingStatus.Equals("Completed", StringComparison.OrdinalIgnoreCase);

	private static IEnumerable<StudentAcademic360ExamResultDto> FilterDetailExams(
		IEnumerable<StudentAcademic360ExamResultDto> exams, StudentAcademic360Input input)
	{
		if (input.ExamStatusFilter == "upcoming") exams = exams.Where(exam => !IsExamGradingCompleted(exam));
		if (input.ExamStatusFilter == "completed") exams = exams.Where(IsExamGradingCompleted);
		if (!string.IsNullOrWhiteSpace(input.ExamSearchTerm))
		{
			var search = input.ExamSearchTerm.Trim().ToLowerInvariant();
			exams = exams.Where(exam => exam.ExamName.ToLowerInvariant().Contains(search, StringComparison.Ordinal) ||
				string.Join(" ", exam.Subjects.Select(subject => subject.SubjectName)).ToLowerInvariant().Contains(search, StringComparison.Ordinal));
		}
		return exams;
	}

	private static StudentAcademic360ExamSummaryDto BuildExamSummary(IReadOnlyCollection<StudentAcademic360ExamResultDto> exams)
	{
		var completed = exams.Where(IsExamGradingCompleted).ToList();
		var scores = completed.Where(exam => exam.Percentage > 0 || exam.TotalMarks > 0)
			.Select(exam => exam.Percentage > 0 ? exam.Percentage : exam.ObtainedMarks * 100d / exam.TotalMarks).ToList();
		return new StudentAcademic360ExamSummaryDto
		{
			Total = exams.Count, Upcoming = exams.Count - completed.Count, Completed = completed.Count,
			Average = scores.Count == 0 ? null : Math.Floor(scores.Average() + 0.5)
		};
	}
}
