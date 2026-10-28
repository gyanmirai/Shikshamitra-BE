using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Academic.Reports;

public class AcademicDynamicReportCatalog(
	IAsyncQueryableExecuter asyncExecuter,
	IRepository<AssignedSubject, Guid> assignedSubjectRepository,
	IRepository<Subject, Guid> subjectRepository,
	IRepository<Division, Guid> divisionRepository,
	IRepository<Standard, Guid> standardRepository) : ITransientDependency
{
	private static readonly IReadOnlyDictionary<string, AcademicDynamicDatasetDefinition> DatasetDefinitions =
		new Dictionary<string, AcademicDynamicDatasetDefinition>(StringComparer.OrdinalIgnoreCase)
		{
			[AcademicDynamicDatasets.CurriculumSubjects] = new(
				AcademicDynamicDatasets.CurriculumSubjects,
				AcademicReportModules.Curriculum,
				"Curriculum subjects",
				"One row per subject assigned to a class, including setup, teacher and chapter totals.",
				[
					new("class", "Class"), new("subject", "Subject"), new("subjectCode", "Subject code"),
					new("subjectType", "Subject type"), new("status", "Status"),
					new("weeklyPeriods", "Weekly periods", AcademicDynamicFieldTypes.Number), new("difficultyLevel", "Difficulty level"),
					new("subjectGroup", "Subject group"), new("primaryTeacher", "Primary teacher"),
					new("secondaryTeachers", "Secondary teachers"), new("chapterCount", "Chapter count", AcademicDynamicFieldTypes.Number),
					new("averageProgress", "Average progress", AcademicDynamicFieldTypes.Number), new("setupHealthScore", "Setup health score", AcademicDynamicFieldTypes.Number),
					new("learningGoals", "Learning goals"), new("prerequisites", "Prerequisites"),
					new("description", "Description")
				],
				["class", "subject", "subjectCode", "primaryTeacher", "chapterCount", "averageProgress"],
				[AcademicDynamicFilterTypes.Class, AcademicDynamicFilterTypes.Subject]),
			[AcademicDynamicDatasets.CurriculumChapters] = new(
				AcademicDynamicDatasets.CurriculumChapters,
				AcademicReportModules.Curriculum,
				"Curriculum chapters",
				"One row per curriculum chapter with schedule, progress, coverage and planning details.",
				[
					new("class", "Class"), new("subject", "Subject"), new("chapterNumber", "Chapter number", AcademicDynamicFieldTypes.Number),
					new("chapterName", "Chapter"), new("unitName", "Unit"), new("termName", "Term"),
					new("topic", "Topic"), new("plannedStart", "Planned start", AcademicDynamicFieldTypes.Date), new("plannedEnd", "Planned end", AcademicDynamicFieldTypes.Date),
					new("estimatedTeachingDays", "Estimated teaching days", AcademicDynamicFieldTypes.Number), new("estimatedPeriods", "Estimated periods", AcademicDynamicFieldTypes.Number),
					new("revisionDays", "Revision days", AcademicDynamicFieldTypes.Number), new("progressPercentage", "Progress", AcademicDynamicFieldTypes.Number),
					new("coverageStatus", "Coverage status"), new("publishStatus", "Publish status"),
					new("reviewStatus", "Review status"), new("healthScore", "Health score", AcademicDynamicFieldTypes.Number),
					new("marksWeightage", "Marks weightage", AcademicDynamicFieldTypes.Number), new("difficultyLevel", "Difficulty level"),
					new("bloomLevel", "Bloom level"), new("description", "Description")
				],
				["class", "subject", "chapterNumber", "chapterName", "plannedStart", "plannedEnd", "progressPercentage"],
				[AcademicDynamicFilterTypes.Class, AcademicDynamicFilterTypes.Subject, AcademicDynamicFilterTypes.Chapter]),
			[AcademicDynamicDatasets.CurriculumTeachers] = new(
				AcademicDynamicDatasets.CurriculumTeachers,
				AcademicReportModules.Curriculum,
				"Curriculum subject teachers",
				"One row per teacher assigned to a class subject, including primary and secondary teacher roles.",
				[
					new("class", "Class"), new("subject", "Subject"), new("subjectCode", "Subject code"),
					new("teacherName", "Teacher"), new("teacherCode", "Teacher code"),
					new("teacherType", "Teacher type"), new("weeklyPeriods", "Weekly periods", AcademicDynamicFieldTypes.Number),
					new("subjectStatus", "Subject status")
				],
				["class", "subject", "teacherName", "teacherType"],
				[AcademicDynamicFilterTypes.Class, AcademicDynamicFilterTypes.Subject]),
			[AcademicDynamicDatasets.TimetableSlots] = new(
				AcademicDynamicDatasets.TimetableSlots,
				AcademicReportModules.Timetable,
				"Timetable slots",
				"One row per scheduled timetable subject or break, including class, division, time, teacher and room.",
				[
					new("class", "Class"), new("division", "Division"), new("day", "Day"),
					new("sequence", "Period number", AcademicDynamicFieldTypes.Number),
					new("startTime", "Start time"), new("endTime", "End time"),
					new("durationMinutes", "Duration minutes", AcademicDynamicFieldTypes.Number),
					new("slotType", "Slot type"), new("breakType", "Break type"),
					new("subject", "Subject"), new("teacherName", "Teacher"),
					new("teacherCode", "Teacher code"), new("room", "Room"),
					new("timetableStatus", "Timetable status")
				],
				["class", "division", "day", "sequence", "startTime", "endTime", "subject", "teacherName", "room"],
				[AcademicDynamicFilterTypes.Class, AcademicDynamicFilterTypes.Division, AcademicDynamicFilterTypes.Subject]),
			[AcademicDynamicDatasets.StudentRecordsStudents] = new(
				AcademicDynamicDatasets.StudentRecordsStudents,
				AcademicReportModules.StudentRecords,
				"Student records",
				"One row per student in the selected academic context. This privacy-minimized dataset exposes only school identity, placement and lifecycle fields.",
				[
					new("class", "Class"), new("division", "Division"),
					new("grNumber", "GR number"), new("rollNumber", "Roll number", AcademicDynamicFieldTypes.Number),
					new("studentName", "Student"), new("lifecycleStatus", "Lifecycle status"),
					new("admissionApprovalStatus", "Admission approval status")
				],
				["class", "division", "rollNumber", "grNumber", "studentName", "lifecycleStatus"],
				[AcademicDynamicFilterTypes.Class, AcademicDynamicFilterTypes.Division]),
			[AcademicDynamicDatasets.StudentAttendanceEntries] = new(
				AcademicDynamicDatasets.StudentAttendanceEntries,
				AcademicReportModules.StudentAttendance,
				"Student attendance entries",
				"One row per marked student attendance entry, suitable for absence lists and detailed registers.",
				[
					new("date", "Date", AcademicDynamicFieldTypes.Date), new("class", "Class"),
					new("division", "Division"), new("grNumber", "GR number"),
					new("rollNumber", "Roll number", AcademicDynamicFieldTypes.Number),
					new("studentName", "Student"), new("status", "Attendance status"),
					new("remarks", "Remarks"), new("isLocked", "Day locked")
				],
				["date", "class", "division", "rollNumber", "studentName", "status"],
				[AcademicDynamicFilterTypes.Class, AcademicDynamicFilterTypes.Division]),
			[AcademicDynamicDatasets.StudentAttendanceDailySummary] = new(
				AcademicDynamicDatasets.StudentAttendanceDailySummary,
				AcademicReportModules.StudentAttendance,
				"Student attendance daily summary",
				"One row per date, class and division with marked, present, absent and attendance percentage totals.",
				[
					new("date", "Date", AcademicDynamicFieldTypes.Date), new("class", "Class"),
					new("division", "Division"), new("markedCount", "Marked students", AcademicDynamicFieldTypes.Number),
					new("presentCount", "Present", AcademicDynamicFieldTypes.Number),
					new("absentCount", "Absent", AcademicDynamicFieldTypes.Number),
					new("attendancePercentage", "Attendance percentage", AcademicDynamicFieldTypes.Number),
					new("isLocked", "Day locked")
				],
				["date", "class", "division", "markedCount", "presentCount", "absentCount", "attendancePercentage"],
				[AcademicDynamicFilterTypes.Class, AcademicDynamicFilterTypes.Division]),
			[AcademicDynamicDatasets.HomeworkTasks] = new(
				AcademicDynamicDatasets.HomeworkTasks,
				AcademicReportModules.HomeworkAssignment,
				"Homework tasks",
				"One row per homework and assigned division, including publication, due date and lifecycle details.",
				[
					new("class", "Class"), new("division", "Division"), new("subject", "Subject"),
					new("title", "Homework title"), new("status", "Lifecycle status"),
					new("submissionType", "Submission type"),
					new("publishDate", "Publish date", AcademicDynamicFieldTypes.Date),
					new("dueDate", "Due date", AcademicDynamicFieldTypes.Date),
					new("isPublished", "Published"), new("description", "Description")
				],
				["class", "division", "subject", "title", "status", "publishDate", "dueDate", "isPublished"],
				[AcademicDynamicFilterTypes.Class, AcademicDynamicFilterTypes.Division, AcademicDynamicFilterTypes.Subject]),
			[AcademicDynamicDatasets.AssignmentTasks] = new(
				AcademicDynamicDatasets.AssignmentTasks,
				AcademicReportModules.HomeworkAssignment,
				"Assignments",
				"One row per assignment and assigned division, including publication, marks and evaluation details.",
				[
					new("class", "Class"), new("division", "Division"), new("subject", "Subject"),
					new("title", "Assignment title"), new("status", "Lifecycle status"),
					new("submissionType", "Submission type"),
					new("publishDate", "Publish date", AcademicDynamicFieldTypes.Date),
					new("dueDate", "Due date", AcademicDynamicFieldTypes.Date),
					new("isPublished", "Published"),
					new("totalMarks", "Total marks", AcademicDynamicFieldTypes.Number),
					new("evaluationRequired", "Evaluation required"), new("evaluationMethod", "Evaluation method"),
					new("description", "Description")
				],
				["class", "division", "subject", "title", "status", "dueDate", "totalMarks", "evaluationMethod"],
				[AcademicDynamicFilterTypes.Class, AcademicDynamicFilterTypes.Division, AcademicDynamicFilterTypes.Subject]),
			[AcademicDynamicDatasets.ExaminationSchedule] = new(
				AcademicDynamicDatasets.ExaminationSchedule,
				AcademicReportModules.Examination,
				"Examination schedule",
				"One row per exam subject and participating division with its date, time and marks configuration.",
				[
					new("examName", "Exam"), new("class", "Class"), new("division", "Division"),
					new("subject", "Subject"), new("examDate", "Exam date", AcademicDynamicFieldTypes.Date),
					new("startTime", "Start time"), new("endTime", "End time"),
					new("totalMarks", "Total marks", AcademicDynamicFieldTypes.Number),
					new("passingMarks", "Passing marks", AcademicDynamicFieldTypes.Number),
					new("examStatus", "Exam status"), new("notes", "Notes")
				],
				["examName", "class", "division", "subject", "examDate", "startTime", "endTime", "totalMarks", "passingMarks", "examStatus"],
				[AcademicDynamicFilterTypes.Class, AcademicDynamicFilterTypes.Division, AcademicDynamicFilterTypes.Subject]),
			[AcademicDynamicDatasets.ExaminationMarks] = new(
				AcademicDynamicDatasets.ExaminationMarks,
				AcademicReportModules.Examination,
				"Student examination marks",
				"One row per student and exam subject, scoped to authorized students and published-state metadata.",
				[
					new("examName", "Exam"), new("class", "Class"), new("division", "Division"),
					new("subject", "Subject"), new("grNumber", "GR number"),
					new("rollNumber", "Roll number", AcademicDynamicFieldTypes.Number), new("studentName", "Student"),
					new("attendanceStatus", "Exam attendance"),
					new("marksObtained", "Marks obtained", AcademicDynamicFieldTypes.Number),
					new("totalMarks", "Total marks", AcademicDynamicFieldTypes.Number),
					new("percentage", "Percentage", AcademicDynamicFieldTypes.Number),
					new("result", "Subject result"), new("isPublished", "Marks published"),
					new("feedback", "Feedback")
				],
				["examName", "class", "division", "subject", "rollNumber", "studentName", "attendanceStatus", "marksObtained", "totalMarks", "percentage", "result"],
				[AcademicDynamicFilterTypes.Class, AcademicDynamicFilterTypes.Division, AcademicDynamicFilterTypes.Subject]),
			[AcademicDynamicDatasets.ExaminationResults] = new(
				AcademicDynamicDatasets.ExaminationResults,
				AcademicReportModules.Examination,
				"Student examination results",
				"One row per student result snapshot with totals, grade, rank and publication lifecycle.",
				[
					new("examName", "Exam"), new("class", "Class"), new("division", "Division"),
					new("grNumber", "GR number"), new("rollNumber", "Roll number", AcademicDynamicFieldTypes.Number),
					new("studentName", "Student"),
					new("totalMaxMarks", "Maximum marks", AcademicDynamicFieldTypes.Number),
					new("totalObtainedMarks", "Obtained marks", AcademicDynamicFieldTypes.Number),
					new("percentage", "Percentage", AcademicDynamicFieldTypes.Number), new("grade", "Grade"),
					new("classRank", "Class rank", AcademicDynamicFieldTypes.Number),
					new("sectionRank", "Section rank", AcademicDynamicFieldTypes.Number),
					new("resultStatus", "Student result"), new("publicationStatus", "Publication status"),
					new("generatedOn", "Generated on", AcademicDynamicFieldTypes.Date),
					new("publishedOn", "Published on", AcademicDynamicFieldTypes.Date)
				],
				["examName", "class", "division", "rollNumber", "studentName", "totalObtainedMarks", "percentage", "grade", "sectionRank", "resultStatus", "publicationStatus"],
				[AcademicDynamicFilterTypes.Class, AcademicDynamicFilterTypes.Division])
		};

	public IReadOnlyList<AcademicDynamicDatasetDefinition> GetDatasets(string module) =>
		DatasetDefinitions.Values
			.Where(dataset => string.Equals(dataset.Module, module, StringComparison.OrdinalIgnoreCase))
			.OrderBy(dataset => dataset.Id, StringComparer.OrdinalIgnoreCase)
			.ToList();

	public AcademicDynamicDatasetDefinition GetDataset(string datasetId) =>
		DatasetDefinitions.TryGetValue(datasetId, out var dataset)
			? dataset
			: throw AcademicDynamicReportGuard.DatasetNotAllowed(datasetId);

	public async Task<AcademicDynamicReportCatalogSnapshot> BuildSnapshotAsync(AcademicAiReportCatalogScope scope, string? module = null)
	{
		var standardQuery = await standardRepository.GetQueryableAsync();
		standardQuery = standardQuery.Where(item =>
			item.EducationMediumStage.EducationMediumId == scope.EducationMediumId);
		if (scope.ClassIds is { Count: > 0 })
		{
			standardQuery = standardQuery.Where(item => scope.ClassIds.Contains(item.Id));
		}
		var standards = await asyncExecuter.ToListAsync(standardQuery.OrderBy(item => item.Id).Select(item => new { item.Id, item.Name }));

		var assignmentQuery = await assignedSubjectRepository.GetQueryableAsync();
		var subjectQuery = await subjectRepository.GetQueryableAsync();
		// The former required Subject include excluded invisible subjects from every child dataset.
		var query = from assignment in assignmentQuery
					join subject in subjectQuery on assignment.SubjectId equals subject.Id
					select assignment;
		query = query.Where(item =>
			item.Standard.EducationMediumStage.EducationMediumId == scope.EducationMediumId);
		if (scope.ClassIds is { Count: > 0 })
		{
			query = query.Where(item => scope.ClassIds.Contains(item.StandardId));
		}
		var assignments = await asyncExecuter.ToListAsync(query.OrderBy(item => item.Id)
			.Select(item => new { item.Id, item.SubjectId, SubjectName = item.Subject.Name }));

		var divisionQuery = await divisionRepository.GetQueryableAsync();
		divisionQuery = divisionQuery.Where(item =>
			item.Standard.EducationMediumStage.EducationMediumId == scope.EducationMediumId);
		if (scope.DivisionIds is { Count: > 0 })
		{
			divisionQuery = divisionQuery.Where(item => scope.DivisionIds.Contains(item.Id));
		}
		var divisions = await asyncExecuter.ToListAsync(divisionQuery.Select(item => new { item.Id, item.Name, item.StandardId }));

		var classes = standards
			.Select(item => new CatalogClass(item.Id, item.Name))
			.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
			.Select((item, index) => item with { Token = $"class_{index + 1}" })
			.ToList();
		var classTokens = classes.ToDictionary(item => item.Id, item => item.Token);

		var divisionItems = divisions
			.Where(item => classTokens.ContainsKey(item.StandardId))
			.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
			.Select((item, index) => new CatalogDivision(
				$"division_{index + 1}", item.Id, item.Name, item.StandardId, classTokens[item.StandardId]))
			.ToList();

		var subjects = assignments
			.GroupBy(item => item.SubjectId)
			.Select(group => new CatalogSubject(group.Key, group.First().SubjectName))
			.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
			.Select((item, index) => item with { Token = $"subject_{index + 1}" })
			.ToList();
		var subjectTokens = subjects.ToDictionary(item => item.Id, item => item.Token);

		// Token order uses OrdinalIgnoreCase, so database collation cannot choose the top 1000.
		// Scan narrow scalar pages and retain only those 1000 candidates in memory.
		SortedSet<ChapterCatalogCandidate> chapterCandidates = module == null || string.Equals(
			AcademicDynamicReportGuard.NormalizeModule(module), AcademicReportModules.Curriculum, StringComparison.OrdinalIgnoreCase)
			? await LoadChapterCatalogCandidatesAsync(query)
			: [];
		var chapters = chapterCandidates
			.Select((item, index) => new CatalogChapter(
				$"chapter_{index + 1}", item.Id, item.ChapterName, item.StandardId, item.SubjectId,
				classTokens.GetValueOrDefault(item.StandardId, string.Empty),
				subjectTokens.GetValueOrDefault(item.SubjectId, string.Empty)))
			.ToList();

		return new AcademicDynamicReportCatalogSnapshot(classes, divisionItems, subjects, chapters);
	}

	private async Task<SortedSet<ChapterCatalogCandidate>> LoadChapterCatalogCandidatesAsync(IQueryable<AssignedSubject> assignments)
	{
		var comparer = Comparer<ChapterCatalogCandidate>.Create((left, right) =>
		{
			var result = StringComparer.OrdinalIgnoreCase.Compare(left.ChapterName, right.ChapterName);
			if (result == 0) result = left.AssignmentId.CompareTo(right.AssignmentId);
			if (result == 0) result = left.Id.CompareTo(right.Id);
			return result;
		});
		var selected = new SortedSet<ChapterCatalogCandidate>(comparer);
		var query = from assignment in assignments
					from chapter in assignment.Chapters
					select new ChapterCatalogCandidate
					{
						AssignmentId = assignment.Id, Id = chapter.Id, ChapterName = chapter.ChapterName,
						StandardId = assignment.StandardId, SubjectId = assignment.SubjectId
					};
		ChapterCatalogCandidate? cursor = null;
		while (true)
		{
			var page = query;
			if (cursor != null)
			{
				var assignmentId = cursor.AssignmentId;
				var chapterId = cursor.Id;
				page = page.Where(item => item.AssignmentId.CompareTo(assignmentId) > 0 ||
					(item.AssignmentId == assignmentId && item.Id.CompareTo(chapterId) > 0));
			}
			var items = await asyncExecuter.ToListAsync(page.OrderBy(item => item.AssignmentId).ThenBy(item => item.Id).Take(256));
			if (items.Count == 0) return selected;
			foreach (var item in items)
			{
				selected.Add(item);
				if (selected.Count > 1000) selected.Remove(selected.Max!);
			}
			cursor = items[^1];
		}
	}

	private sealed class ChapterCatalogCandidate
	{
		public Guid AssignmentId { get; set; }
		public Guid Id { get; set; }
		public string ChapterName { get; set; } = string.Empty;
		public Guid StandardId { get; set; }
		public Guid SubjectId { get; set; }
	}
}

public sealed record AcademicDynamicDatasetDefinition(
	string Id,
	string Module,
	string Name,
	string Description,
	IReadOnlyList<AcademicDynamicColumnDefinition> Columns,
	IReadOnlyList<string> DefaultColumns,
	IReadOnlyList<string>? FilterTypes = null)
{
	public IReadOnlyList<string> SupportedFilterTypes => FilterTypes ?? AcademicDynamicFilterTypes.All;
}

public sealed record AcademicDynamicColumnDefinition(
	string Id,
	string Label,
	string DataType = AcademicDynamicFieldTypes.Text);

public static class AcademicDynamicFieldTypes
{
	public const string Text = "text";
	public const string Number = "number";
	public const string Date = "date";
}

public sealed record AcademicDynamicReportCatalogSnapshot(
	IReadOnlyList<CatalogClass> Classes,
	IReadOnlyList<CatalogDivision> Divisions,
	IReadOnlyList<CatalogSubject> Subjects,
	IReadOnlyList<CatalogChapter> Chapters);

public sealed record CatalogClass(Guid Id, string Name, string Token = "");
public sealed record CatalogDivision(string Token, Guid Id, string Name, Guid StandardId, string ClassToken);
public sealed record CatalogSubject(Guid Id, string Name, string Token = "");
public sealed record CatalogChapter(
	string Token,
	Guid Id,
	string Name,
	Guid StandardId,
	Guid SubjectId,
	string ClassToken,
	string SubjectToken);
