using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Academic.Examination.Results;
using ShikshaMitra.Academic.Homework;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students;
using ShikshaMitra.Students.Attendance;
using ShikshaMitra.Students.StudentEnrollments;
using Volo.Abp;

namespace ShikshaMitra.Academic.StudentAcademic360;

public partial class StudentAcademic360AppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Dashboard.View)]
	public async Task<StudentAcademic360ListResultDto> GetListAsync(GetStudentAcademic360ListInput input)
	{
		await studentScopeAccessService.EnsureCanAccessAcademicYearAsync(input.AcademicYearId, nameof(GetListAsync));
		await studentScopeAccessService.EnsureCanAccessMediumAsync(input.EducationMediumId, nameof(GetListAsync));

		if (input.StandardId.HasValue)
		{
			await studentScopeAccessService.EnsureCanAccessStandardAsync(input.StandardId.Value, nameof(GetListAsync));
		}

		if (input.DivisionId.HasValue)
		{
			await studentScopeAccessService.EnsureCanAccessDivisionAsync(input.DivisionId.Value, nameof(GetListAsync));
		}
		await EnsureListContextAsync(input);

		var studentQuery = await studentRepository.GetQueryableWithoutAutoIncludesAsync();
		studentQuery = await studentScopeAccessService.ApplyCurrentUserScopeAsync(studentQuery);
		studentQuery = studentQuery.Where(student =>
			student.AcademicYearId == input.AcademicYearId &&
			student.AcademicBoardId == input.BoardId &&
			student.AcademicMediumId == input.EducationMediumId &&
			student.StandardId.HasValue &&
			student.DivisionId.HasValue &&
			student.Status == StudentLifeCycleStatusEnum.Enrolled);

		if (input.StandardId.HasValue)
		{
			studentQuery = studentQuery.Where(student => student.StandardId == input.StandardId.Value);
		}

		if (input.DivisionId.HasValue)
		{
			studentQuery = studentQuery.Where(student => student.DivisionId == input.DivisionId.Value);
		}

		var students = await AsyncExecuter.ToListAsync(studentQuery.Select(student => new StudentListRead
		{
			StudentId = student.Id,
			StudentName = string.IsNullOrEmpty(student.MiddleName)
				? ((student.FirstName ?? string.Empty) + " " + (student.LastName ?? string.Empty)).Trim()
				: ((student.FirstName ?? string.Empty) + " " + student.MiddleName + " " +
					(student.LastName ?? string.Empty)).Trim(),
			HasPhoto = student.PhotoBlobName != null && student.PhotoBlobName != string.Empty,
			GrNo = student.GrNo,
			RollNo = student.RollNo,
			StandardId = student.StandardId!.Value,
			StandardName = student.Standard != null ? student.Standard.Name : string.Empty,
			DivisionId = student.DivisionId!.Value,
			DivisionName = student.Division != null ? student.Division.Name : string.Empty,
			LastUpdatedAt = student.LastModificationTime ?? student.CreationTime
		}));

		if (students.Count == 0)
		{
			return new StudentAcademic360ListResultDto();
		}

		var studentIds = students.Select(student => student.StudentId).ToList();
		var divisionIds = students.Select(student => student.DivisionId).Distinct().ToList();
		var attendance = await LoadAttendanceSummariesAsync(studentIds, input.AcademicYearId);
		var learning = await LoadLearningSummariesAsync(students, studentIds, divisionIds, input.AcademicYearId);
		var exams = await LoadExamSummariesAsync(studentIds);
		var items = students.Select(student => BuildListItem(student, attendance, learning, exams)).ToList();
		var summary = BuildListSummary(items);

		IEnumerable<StudentAcademic360ListItemDto> filtered = items;
		if (!string.IsNullOrWhiteSpace(input.SearchTerm))
		{
			var search = input.SearchTerm.Trim();
			filtered = filtered.Where(item =>
				item.StudentName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
				(item.GrNo?.ToString().Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
				(item.RollNo?.ToString().Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
				item.StandardName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
				item.DivisionName.Contains(search, StringComparison.OrdinalIgnoreCase));
		}

		filtered = ApplySmartFilter(filtered, input.SmartFilter);
		var ordered = ApplyListSorting(filtered, input.Sorting);
		var totalCount = ordered.LongCount();
		var page = ordered.Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

		return new StudentAcademic360ListResultDto
		{
			TotalCount = totalCount,
			Items = page,
			Summary = summary
		};
	}

	private async Task EnsureListContextAsync(GetStudentAcademic360ListInput input)
	{
		var mediums = await educationMediumRepository.GetQueryableAsync();
		var validMedium = await AsyncExecuter.AnyAsync(mediums.Where(medium =>
			medium.Id == input.EducationMediumId && medium.EducationBoardId == input.BoardId &&
			medium.EducationBoard.AcademicYearId == input.AcademicYearId));
		if (!validMedium)
		{
			throw new BusinessException("Academic:StudentAcademic360:InvalidContext")
				.WithData("AcademicYearId", input.AcademicYearId)
				.WithData("BoardId", input.BoardId)
				.WithData("EducationMediumId", input.EducationMediumId);
		}

		if (input.StandardId.HasValue)
		{
			var standards = await standardRepository.GetQueryableAsync();
			var validStandard = await AsyncExecuter.AnyAsync(standards.Where(standard =>
				standard.Id == input.StandardId.Value &&
				standard.EducationMediumStage.EducationMediumId == input.EducationMediumId));
			if (!validStandard)
			{
				throw new BusinessException("Academic:StudentAcademic360:InvalidStandardContext")
					.WithData("StandardId", input.StandardId.Value);
			}
		}

		if (input.DivisionId.HasValue)
		{
			var divisions = await divisionRepository.GetQueryableAsync();
			var validDivision = await AsyncExecuter.AnyAsync(divisions.Where(division =>
				division.Id == input.DivisionId.Value && division.StandardId == input.StandardId!.Value));
			if (!validDivision)
			{
				throw new BusinessException("Academic:StudentAcademic360:InvalidDivisionContext")
					.WithData("DivisionId", input.DivisionId.Value);
			}
		}
	}

	private async Task<Dictionary<Guid, AttendanceSummaryRead>> LoadAttendanceSummariesAsync(
		IReadOnlyCollection<Guid> studentIds,
		Guid academicYearId)
	{
		var (startDate, endDate) = ResolveAttendanceDateRange(null, null);
		var days = await studentScopeAccessService.ApplyCurrentUserAttendanceDayScopeAsync(
			await attendanceDayRepository.GetQueryableAsync());
		var entries = await attendanceEntryRepository.GetQueryableAsync();
		var rows = await AsyncExecuter.ToListAsync(
			from entry in entries
			join day in days on entry.AttendanceDayId equals day.Id
			where studentIds.Contains(entry.StudentId) && day.AcademicYearId == academicYearId &&
				day.Date >= startDate && day.Date <= endDate
			select new AttendanceStatusRead
			{
				StudentId = entry.StudentId,
				Status = entry.Status,
				UpdatedAt = entry.LastModificationTime ?? entry.CreationTime
			});

		return rows.GroupBy(row => row.StudentId).ToDictionary(group => group.Key, group =>
		{
			var present = group.Count(row => row.Status == AttendanceStatus.Present);
			var absent = group.Count(row => row.Status == AttendanceStatus.Absent);
			var counted = present + absent;
			return new AttendanceSummaryRead
			{
				Present = present,
				Absent = absent,
				Pending = group.Count(row => row.Status == AttendanceStatus.Pending),
				Total = counted,
				Percentage = counted == 0 ? null : Math.Round(present * 100d / counted, 2),
				LastUpdatedAt = group.Max(row => row.UpdatedAt)
			};
		});
	}

	private async Task<Dictionary<Guid, LearningSummaryRead>> LoadLearningSummariesAsync(
		IReadOnlyCollection<StudentListRead> students,
		IReadOnlyCollection<Guid> studentIds,
		IReadOnlyCollection<Guid> divisionIds,
		Guid academicYearId)
	{
		var homeworkQuery = await homeworkRepository.GetQueryableAsync();
		var scheduledHomeworkQuery =
			from homework in homeworkQuery
			from schedule in homework.DivisionSchedules
			where homework.StandardId != Guid.Empty &&
				(!homework.AcademicYearId.HasValue || homework.AcademicYearId == academicYearId) &&
				schedule.IsPublished && divisionIds.Contains(schedule.DivisionId)
			select new LearningTaskScopeRead
				{
					TaskId = homework.Id,
					DivisionId = schedule.DivisionId,
					DueDate = schedule.DueDate,
					UpdatedAt = schedule.PublishedAt ?? homework.LastModificationTime ?? homework.CreationTime,
					IsAssignment = false
				};
		var scheduledHomework = await AsyncExecuter.ToListAsync(scheduledHomeworkQuery);

		var scheduledHomeworkKeys = scheduledHomework.Select(task => (task.TaskId, task.DivisionId)).ToHashSet();
		var legacyHomework = await AsyncExecuter.ToListAsync(homeworkQuery
			.Where(homework => homework.DivisionId.HasValue && divisionIds.Contains(homework.DivisionId.Value) &&
				(!homework.AcademicYearId.HasValue || homework.AcademicYearId == academicYearId) &&
				(homework.IsPublished || homework.Status == HomeworkStatus.Published))
			.Select(homework => new LearningTaskScopeRead
			{
				TaskId = homework.Id,
				DivisionId = homework.DivisionId!.Value,
				DueDate = homework.DueDate,
				UpdatedAt = homework.LastModificationTime ?? homework.CreationTime,
				IsAssignment = false
			}));
		legacyHomework.RemoveAll(task => scheduledHomeworkKeys.Contains((task.TaskId, task.DivisionId)));

		var assignmentQuery = await assignmentRepository.GetQueryableAsync();
		var scheduledAssignmentQuery =
			from assignment in assignmentQuery
			from schedule in assignment.DivisionSchedules
			where (!assignment.AcademicYearId.HasValue || assignment.AcademicYearId == academicYearId) &&
				schedule.IsPublished && divisionIds.Contains(schedule.DivisionId)
			select new LearningTaskScopeRead
				{
					TaskId = assignment.Id,
					DivisionId = schedule.DivisionId,
					DueDate = schedule.DueDate,
					UpdatedAt = schedule.PublishedAt ?? assignment.LastModificationTime ?? assignment.CreationTime,
					IsAssignment = true
				};
		var scheduledAssignments = await AsyncExecuter.ToListAsync(scheduledAssignmentQuery);

		var tasks = scheduledHomework.Concat(legacyHomework).Concat(scheduledAssignments)
			.GroupBy(task => new { task.TaskId, task.DivisionId, task.IsAssignment })
			.Select(group => group.First())
			.ToList();
		var homeworkIds = tasks.Where(task => !task.IsAssignment).Select(task => task.TaskId).Distinct().ToList();
		var assignmentIds = tasks.Where(task => task.IsAssignment).Select(task => task.TaskId).Distinct().ToList();
		var submissionQuery = await submissionRepository.GetQueryableAsync();
		var submissions = await AsyncExecuter.ToListAsync(submissionQuery.Where(submission =>
				studentIds.Contains(submission.StudentId) &&
				((submission.HomeworkAssignmentId.HasValue && homeworkIds.Contains(submission.HomeworkAssignmentId.Value)) ||
				 (submission.AssignmentId.HasValue && assignmentIds.Contains(submission.AssignmentId.Value))))
			.Select(submission => new SubmissionSummaryRead
			{
				StudentId = submission.StudentId,
				TaskId = submission.AssignmentId ?? submission.HomeworkAssignmentId!.Value,
				IsAssignment = submission.AssignmentId.HasValue,
				Status = submission.Status,
				UpdatedAt = submission.LastModificationTime ?? submission.CreationTime
			}));
		var latestSubmissions = submissions
			.GroupBy(submission => new { submission.StudentId, submission.TaskId, submission.IsAssignment })
			.ToDictionary(group => (group.Key.StudentId, group.Key.TaskId, group.Key.IsAssignment),
				group => group.OrderByDescending(submission => submission.UpdatedAt).First());
		var tasksByDivision = tasks.ToLookup(task => task.DivisionId);
		var result = new Dictionary<Guid, LearningSummaryRead>();

		foreach (var student in students)
		{
			var assigned = tasksByDivision[student.DivisionId].ToList();
			if (assigned.Count == 0)
			{
				continue;
			}

			var statuses = assigned.Select(task =>
			{
				latestSubmissions.TryGetValue((student.StudentId, task.TaskId, task.IsAssignment), out var submission);
				return new
				{
					Status = submission?.Status ?? (task.DueDate.HasValue && task.DueDate.Value.Date < Clock.Now.Date
						? SubmissionStatus.Late
						: SubmissionStatus.NotSubmitted),
					UpdatedAt = submission?.UpdatedAt ?? task.UpdatedAt
				};
			}).ToList();
			result[student.StudentId] = new LearningSummaryRead
			{
				Total = statuses.Count,
				Submitted = statuses.Count(row => IsSubmitted(row.Status)),
				Pending = statuses.Count(row => row.Status is SubmissionStatus.NotSubmitted or SubmissionStatus.Draft),
				Late = statuses.Count(row => row.Status == SubmissionStatus.Late),
				LastUpdatedAt = statuses.Max(row => row.UpdatedAt)
			};
		}

		return result;
	}

	private async Task<Dictionary<Guid, ExamSummaryRead>> LoadExamSummariesAsync(IReadOnlyCollection<Guid> studentIds)
	{
		var headers = await resultHeaderRepository.GetQueryableAsync();
		var details = await resultDetailRepository.GetQueryableAsync();
		var resultRows = await AsyncExecuter.ToListAsync(
			from detail in details
			join header in headers on detail.ResultHeaderId equals header.Id
			where studentIds.Contains(detail.StudentId) &&
				(header.Status == ExamResultStatus.Published || header.Status == ExamResultStatus.Frozen)
			select new ExamResultRead
			{
				StudentId = detail.StudentId,
				ExamId = detail.ExamId,
				Percentage = detail.Percentage,
				IsFailed = detail.ResultStatus == ExamStudentResultStatus.Fail,
				UpdatedAt = header.PublishedOn ?? header.FrozenOn ?? detail.LastModificationTime ?? detail.CreationTime
			});

		var markQuery = await examMarkRepository.GetQueryableAsync();
		var marks = await AsyncExecuter.ToListAsync(markQuery.Where(mark =>
				studentIds.Contains(mark.StudentId) && mark.IsPublished)
			.Select(mark => new PublishedMarkRead
			{
				StudentId = mark.StudentId,
				ExamId = mark.ExamId,
				MarksObtained = mark.IsAbsent ? 0 : mark.MarksObtained ?? 0,
				UpdatedAt = mark.LastModificationTime ?? mark.CreationTime
			}));
		var examIds = resultRows.Select(row => row.ExamId).Concat(marks.Select(mark => mark.ExamId)).Distinct().ToList();
		if (examIds.Count == 0)
		{
			return [];
		}

		var examQuery = await examRepository.GetQueryableAsync();
		var exams = await AsyncExecuter.ToListAsync(examQuery.Where(exam => examIds.Contains(exam.Id))
			.Select(exam => new ExamDefinitionRead
			{
				ExamId = exam.Id,
				CreationTime = exam.CreationTime,
				TotalMarks = exam.ExamSubjects.Sum(subject => subject.TotalMarks ?? 0)
			}));
		var examById = exams.ToDictionary(exam => exam.ExamId);
		var existingResults = resultRows.Select(row => (row.StudentId, row.ExamId)).ToHashSet();
		foreach (var markGroup in marks.GroupBy(mark => new { mark.StudentId, mark.ExamId }))
		{
			if (existingResults.Contains((markGroup.Key.StudentId, markGroup.Key.ExamId)) ||
				!examById.TryGetValue(markGroup.Key.ExamId, out var exam) || exam.TotalMarks <= 0)
			{
				continue;
			}

			resultRows.Add(new ExamResultRead
			{
				StudentId = markGroup.Key.StudentId,
				ExamId = markGroup.Key.ExamId,
				Percentage = Math.Round(markGroup.Sum(mark => mark.MarksObtained) * 100d / exam.TotalMarks, 2),
				UpdatedAt = markGroup.Max(mark => mark.UpdatedAt)
			});
		}

		return resultRows.GroupBy(row => row.StudentId).ToDictionary(group => group.Key, group =>
		{
			var ordered = group.OrderByDescending(row => examById.GetValueOrDefault(row.ExamId)?.CreationTime ?? row.UpdatedAt).ToList();
			var latest = ordered.First().Percentage;
			var oldest = ordered.Last().Percentage;
			return new ExamSummaryRead
			{
				Count = ordered.Count,
				Average = Math.Round(ordered.Average(row => row.Percentage), 2),
				Trend = ordered.Count < 2 ? "Insufficient Data" : latest >= oldest ? "Improving" : "Declining",
				HasFailedResult = ordered.Any(row => row.IsFailed),
				LastUpdatedAt = ordered.Max(row => row.UpdatedAt)
			};
		});
	}

	private static StudentAcademic360ListItemDto BuildListItem(
		StudentListRead student,
		IReadOnlyDictionary<Guid, AttendanceSummaryRead> attendanceByStudent,
		IReadOnlyDictionary<Guid, LearningSummaryRead> learningByStudent,
		IReadOnlyDictionary<Guid, ExamSummaryRead> examsByStudent)
	{
		attendanceByStudent.TryGetValue(student.StudentId, out var attendance);
		learningByStudent.TryGetValue(student.StudentId, out var learning);
		examsByStudent.TryGetValue(student.StudentId, out var exams);
		var hasAttendance = attendance?.Total > 0;
		var hasLearning = learning?.Total > 0;
		var hasExams = exams?.Count > 0;
		var sourceCount = (hasAttendance ? 1 : 0) + (hasLearning ? 1 : 0) + (hasExams ? 1 : 0);
		var health = BuildListHealth(attendance, learning, exams);
		var reasons = new List<string>();
		if (attendance?.Percentage < 75) reasons.Add("Attendance is below 75%.");
		if (learning is { Pending: > 0 }) reasons.Add($"{learning.Pending} learning tasks are pending.");
		if (learning is { Late: > 0 }) reasons.Add($"{learning.Late} learning tasks are late.");
		if (exams?.HasFailedResult == true) reasons.Add("A published exam result requires attention.");
		var alertCount = reasons.Count;
		var lastUpdated = new[] { student.LastUpdatedAt, attendance?.LastUpdatedAt, learning?.LastUpdatedAt, exams?.LastUpdatedAt }
			.Where(value => value.HasValue).Max();

		return new StudentAcademic360ListItemDto
		{
			StudentId = student.StudentId,
			StudentName = student.StudentName,
			PhotoUrl = student.HasPhoto ? $"/api/students/records/{student.StudentId}/photo" : null,
			GrNo = student.GrNo,
			RollNo = student.RollNo,
			StandardId = student.StandardId,
			StandardName = student.StandardName,
			DivisionId = student.DivisionId,
			DivisionName = student.DivisionName,
			Attendance = hasAttendance ? new StudentAcademic360ListMetricDto
			{
				Percentage = attendance!.Percentage,
				Completed = attendance.Present,
				Total = attendance.Total,
				Pending = attendance.Pending,
				Status = "Attendance"
			} : null,
			Homework = hasLearning ? new StudentAcademic360ListMetricDto
			{
				Percentage = Math.Round(learning!.Submitted * 100d / learning.Total, 2),
				Completed = learning.Submitted,
				Total = learning.Total,
				Pending = learning.Pending,
				Status = learning.Late > 0 ? $"{learning.Late} late" : "Learning tasks"
			} : null,
			ExamAverage = hasExams ? new StudentAcademic360ListMetricDto
			{
				Percentage = exams!.Average,
				Count = exams.Count,
				Status = "Exams"
			} : null,
			Trend = exams?.Trend ?? "Insufficient Data",
			AcademicHealth = health,
			AcademicHealthReasons = reasons,
			AlertCount = alertCount,
			DataCompleteness = sourceCount == 3 ? "Complete" : sourceCount == 2 ? "Partial data" :
				sourceCount == 1 ? "Limited data" : "Insufficient Data",
			LastUpdatedAt = lastUpdated
		};
	}

	private static string BuildListHealth(
		AttendanceSummaryRead? attendance,
		LearningSummaryRead? learning,
		ExamSummaryRead? exams)
	{
		if (attendance?.Total is not > 0 && learning?.Total is not > 0 && exams?.Count is not > 0)
		{
			return "Insufficient Data";
		}

		var attendanceEarned = attendance?.Total is > 0 ? (int)Math.Round(attendance.Percentage!.Value * 25 / 100) : 0;
		var homeworkEarned = learning?.Total is > 0 ? (int)Math.Round(learning.Submitted * 25d / learning.Total) : 0;
		var examEarned = exams?.Count is > 0 ? (int)Math.Round(exams.Average * 30 / 100) : 0;
		var consistencyEarned = (learning?.Late ?? 0) == 0 && (attendance?.Absent ?? 0) <= 2 ? 10 : 5;
		var improvementEarned = exams?.Count >= 2 && exams.Trend == "Improving" ? 10 : exams?.Count > 0 ? 6 : 0;
		var score = attendanceEarned + homeworkEarned + examEarned + consistencyEarned + improvementEarned;
		return score >= 80 ? "On Track" : score >= 65 ? "Improving" : score >= 50 ? "Needs Attention" : "Support Recommended";
	}

	private static StudentAcademic360ListSummaryDto BuildListSummary(IReadOnlyCollection<StudentAcademic360ListItemDto> items)
	{
		var attendance = items.Where(item => item.Attendance?.Percentage.HasValue == true)
			.Select(item => item.Attendance!.Percentage!.Value).ToList();
		var homework = items.Where(item => item.Homework?.Percentage.HasValue == true)
			.Select(item => item.Homework!.Percentage!.Value).ToList();
		var exams = items.Where(item => item.ExamAverage?.Percentage.HasValue == true)
			.Select(item => item.ExamAverage!.Percentage!.Value).ToList();
		return new StudentAcademic360ListSummaryDto
		{
			TotalStudents = items.Count,
			NeedsAttention = items.Count(item => item.AcademicHealth is "Needs Attention" or "Support Recommended"),
			AverageAttendance = attendance.Count == 0 ? null : Math.Round(attendance.Average(), 2),
			HomeworkCompletion = homework.Count == 0 ? null : Math.Round(homework.Average(), 2),
			AverageExamScore = exams.Count == 0 ? null : Math.Round(exams.Average(), 2)
		};
	}

	private static IEnumerable<StudentAcademic360ListItemDto> ApplySmartFilter(
		IEnumerable<StudentAcademic360ListItemDto> items,
		StudentAcademic360SmartFilter filter) => filter switch
	{
		StudentAcademic360SmartFilter.NeedsAttention => items.Where(item =>
			item.AcademicHealth is "Needs Attention" or "Support Recommended"),
		StudentAcademic360SmartFilter.LowAttendance => items.Where(item => item.Attendance?.Percentage < 75),
		StudentAcademic360SmartFilter.OverdueHomework => items.Where(item =>
			item.Homework?.Status?.Contains("late", StringComparison.OrdinalIgnoreCase) == true || item.Homework?.Pending > 0),
		StudentAcademic360SmartFilter.PerformanceDecline => items.Where(item => item.Trend == "Declining"),
		StudentAcademic360SmartFilter.NoData => items.Where(item => item.DataCompleteness == "Insufficient Data"),
		_ => items
	};

	private static IOrderedEnumerable<StudentAcademic360ListItemDto> ApplyListSorting(
		IEnumerable<StudentAcademic360ListItemDto> items,
		string? sorting)
	{
		var normalized = sorting?.Trim().ToLowerInvariant();
		var descending = normalized?.EndsWith(" desc", StringComparison.Ordinal) == true;
		var field = normalized?.Replace(" desc", string.Empty, StringComparison.Ordinal)
			.Replace(" asc", string.Empty, StringComparison.Ordinal);
		return (field, descending) switch
		{
			("name" or "studentname", true) => items.OrderByDescending(item => item.StudentName),
			("name" or "studentname", false) => items.OrderBy(item => item.StudentName),
			("attendance.percentage" or "attendance", true) => items.OrderByDescending(item => item.Attendance?.Percentage),
			("attendance.percentage" or "attendance", false) => items.OrderBy(item => item.Attendance?.Percentage),
			("homework.percentage" or "homework", true) => items.OrderByDescending(item => item.Homework?.Percentage),
			("homework.percentage" or "homework", false) => items.OrderBy(item => item.Homework?.Percentage),
			("examaverage.percentage" or "examaverage", true) => items.OrderByDescending(item => item.ExamAverage?.Percentage),
			("examaverage.percentage" or "examaverage", false) => items.OrderBy(item => item.ExamAverage?.Percentage),
			("trend", true) => items.OrderByDescending(item => item.Trend),
			("trend", false) => items.OrderBy(item => item.Trend),
			("academichealth", true) => items.OrderByDescending(item => item.AcademicHealth),
			("academichealth", false) => items.OrderBy(item => item.AcademicHealth),
			("alertcount", true) => items.OrderByDescending(item => item.AlertCount),
			("alertcount", false) => items.OrderBy(item => item.AlertCount),
			("lastupdatedat", true) => items.OrderByDescending(item => item.LastUpdatedAt),
			("lastupdatedat", false) => items.OrderBy(item => item.LastUpdatedAt),
			_ => items.OrderBy(item => item.RollNo ?? int.MaxValue).ThenBy(item => item.StudentName)
		};
	}

	private sealed class StudentListRead
	{
		public Guid StudentId { get; init; }
		public string StudentName { get; init; } = string.Empty;
		public bool HasPhoto { get; init; }
		public int? GrNo { get; init; }
		public int? RollNo { get; init; }
		public Guid StandardId { get; init; }
		public string StandardName { get; init; } = string.Empty;
		public Guid DivisionId { get; init; }
		public string DivisionName { get; init; } = string.Empty;
		public DateTime LastUpdatedAt { get; init; }
	}

	private sealed class AttendanceStatusRead
	{
		public Guid StudentId { get; init; }
		public AttendanceStatus Status { get; init; }
		public DateTime UpdatedAt { get; init; }
	}

	private sealed class AttendanceSummaryRead
	{
		public int Present { get; init; }
		public int Absent { get; init; }
		public int Pending { get; init; }
		public int Total { get; init; }
		public double? Percentage { get; init; }
		public DateTime LastUpdatedAt { get; init; }
	}

	private sealed class LearningTaskScopeRead
	{
		public Guid TaskId { get; init; }
		public Guid DivisionId { get; init; }
		public DateTime? DueDate { get; init; }
		public DateTime UpdatedAt { get; init; }
		public bool IsAssignment { get; init; }
	}

	private sealed class SubmissionSummaryRead
	{
		public Guid StudentId { get; init; }
		public Guid TaskId { get; init; }
		public bool IsAssignment { get; init; }
		public SubmissionStatus Status { get; init; }
		public DateTime UpdatedAt { get; init; }
	}

	private sealed class LearningSummaryRead
	{
		public int Total { get; init; }
		public int Submitted { get; init; }
		public int Pending { get; init; }
		public int Late { get; init; }
		public DateTime LastUpdatedAt { get; init; }
	}

	private sealed class ExamResultRead
	{
		public Guid StudentId { get; init; }
		public Guid ExamId { get; init; }
		public double Percentage { get; init; }
		public bool IsFailed { get; init; }
		public DateTime UpdatedAt { get; init; }
	}

	private sealed class PublishedMarkRead
	{
		public Guid StudentId { get; init; }
		public Guid ExamId { get; init; }
		public double MarksObtained { get; init; }
		public DateTime UpdatedAt { get; init; }
	}

	private sealed class ExamDefinitionRead
	{
		public Guid ExamId { get; init; }
		public DateTime CreationTime { get; init; }
		public double TotalMarks { get; init; }
	}

	private sealed class ExamSummaryRead
	{
		public int Count { get; init; }
		public double Average { get; init; }
		public string Trend { get; init; } = "Insufficient Data";
		public bool HasFailedResult { get; init; }
		public DateTime LastUpdatedAt { get; init; }
	}
}
