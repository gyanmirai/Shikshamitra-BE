using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.Academic.QuestionBank;
using ShikshaMitra.Academic.QuestionPapers;
using ShikshaMitra.Academic.Examination.SeatAllocation;
using ShikshaMitra.Academic.Timetable;
using ShikshaMitra.Common;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.Dashboard;

public partial class AcademicDashboardAppService
{
	private async Task<DashboardSectionSnapshot> ReadDashboardSectionAggregatesAsync(bool teacherScope)
	{
		Guid? teacherId = null;
		if (teacherScope && CurrentUser.Id.HasValue)
		{
			var employeeQuery = await _employeeRepository.GetQueryableAsync();
			teacherId = await AsyncExecuter.FirstOrDefaultAsync(employeeQuery
				.Where(employee => employee.UserId == CurrentUser.Id.Value)
				.Select(employee => (Guid?)employee.Id));
		}

		var allocations = await _assignedSubjectRepository.GetQueryableAsync();
		if (teacherScope)
		{
			allocations = teacherId.HasValue
				? allocations.Where(assignment => assignment.Teachers.Any(teacher => teacher.TeacherId == teacherId.Value))
				: allocations.Where(_ => false);
		}
		var standardIds = allocations.Select(assignment => assignment.StandardId);
		var subjectIds = allocations.Select(assignment => assignment.SubjectId);
		var divisions = await _divisionRepository.GetQueryableAsync();
		var subjects = await _subjectRepository.GetQueryableAsync();
		var exams = await _examRepository.GetQueryableAsync();
		var homeworks = await _homeworkRepository.GetQueryableAsync();
		var assignments = await _assignmentRepository.GetQueryableAsync();
		var questions = await _questionBankRepository.GetQueryableAsync();
		var papers = await _questionPaperRepository.GetQueryableAsync();
		if (teacherScope)
		{
			divisions = divisions.Where(division => standardIds.Contains(division.StandardId));
			subjects = subjects.Where(subject => subjectIds.Contains(subject.Id));
			exams = exams.Where(exam => standardIds.Contains(exam.StandardId));
			homeworks = homeworks.Where(homework => standardIds.Contains(homework.StandardId) && subjectIds.Contains(homework.SubjectId));
			assignments = assignments.Where(assignment => standardIds.Contains(assignment.StandardId) && subjectIds.Contains(assignment.SubjectId));
			questions = questions.Where(question => standardIds.Contains(question.StandardId) && subjectIds.Contains(question.SubjectId));
			papers = papers.Where(paper => standardIds.Contains(paper.StandardId) && subjectIds.Contains(paper.SubjectId));
		}
		var divisionIds = divisions.Select(division => division.Id);
		var timetables = await _timetableRepository.GetQueryableAsync();
		var submissions = await _submissionRepository.GetQueryableAsync();
		var results = await _examResultDetailRepository.GetQueryableAsync();
		if (teacherScope)
		{
			timetables = timetables.Where(timetable => divisionIds.Contains(timetable.DivisionId));
			var homeworkIds = homeworks.Select(homework => homework.Id);
			submissions = submissions.Where(submission => divisionIds.Contains(submission.DivisionId) &&
				(!submission.HomeworkAssignmentId.HasValue || homeworkIds.Contains(submission.HomeworkAssignmentId.Value)));
			results = results.Where(result => divisionIds.Contains(result.DivisionId));
		}

		var examCounts = await AsyncExecuter.ToListAsync(exams.GroupBy(exam => exam.Status)
			.Select(group => new { Status = group.Key, Count = group.Count() }));
		var timetableCounts = await AsyncExecuter.ToListAsync(timetables.GroupBy(timetable => timetable.Status)
			.Select(group => new { Status = group.Key, Count = group.Count() }));
		var submissionRows = await AsyncExecuter.ToListAsync(submissions.GroupBy(submission => submission.Status)
			.Select(group => new { Status = group.Key, Count = group.Count() }));
		var submissionCounts = submissionRows.ToDictionary(item => item.Status, item => item.Count);
		var pendingEvaluations = submissionCounts.GetValueOrDefault(SubmissionStatus.NeedsReview) +
			submissionCounts.GetValueOrDefault(SubmissionStatus.Submitted) + submissionCounts.GetValueOrDefault(SubmissionStatus.Late);
		var pendingTimetables = timetableCounts.Where(item => item.Status == TimetableStatus.Draft).Sum(item => item.Count);
		var pendingGradeEntries = examCounts.Where(item => item.Status == ExamStatus.Published).Sum(item => item.Count);

		// A missing employee keeps the legacy null-filter behavior for lesson and diary totals.
		var lessonPlans = await GetDashboardLessonPlanCountsAsync(teacherId);
		var diaries = await GetDashboardDiaryCountsAsync(teacherId);
		var questionCounts = await AsyncExecuter.ToListAsync(questions.GroupBy(question => question.Status)
			.Select(group => new DashboardQuestionCount(group.Key, group.Count())));
		var paperCounts = await AsyncExecuter.ToListAsync(papers.GroupBy(paper => new { paper.Status, paper.IsLocked })
			.Select(group => new DashboardPaperCount(group.Key.Status, group.Key.IsLocked, group.Count())));
		var seatingCounts = await GetDashboardSeatingCountsAsync();
		var calendarImpacts = await _calendarImpactRepository.CountAsync(impact => !impact.IsResolved);
		var students = await _studentRepository.GetQueryableAsync();
		var studentRows = await AsyncExecuter.ToListAsync(students.GroupBy(_ => 1)
			.Select(group => new DashboardStudentCounts(group.Count(),
				group.Count(student => student.StandardId.HasValue && student.DivisionId.HasValue))));
		var studentCounts = studentRows.SingleOrDefault();
		var allocationRows = await AsyncExecuter.ToListAsync(allocations.GroupBy(_ => 1)
			.Select(group => new
			{
				Count = group.Count(),
				Ready = group.Count(assignment => assignment.Status != SubjectStatus.Archived &&
					assignment.Teachers.Any(teacher => teacher.TeacherType == SubjectTeacherType.Primary))
			}));
		var allocationCounts = allocationRows.SingleOrDefault();
		var progress = await GetTenantDashboardProgressQueryAsync();
		var metrics = new DashboardReadinessMetrics
		{
			StudentCount = studentCounts?.Total ?? 0,
			MappedStudentCount = studentCounts?.Mapped ?? 0,
			TeacherCount = await _employeeRepository.CountAsync(employee => employee.EmployeeGroup == EmployeeGroupType.Teaching),
			SubjectCount = await AsyncExecuter.CountAsync(subjects),
			AssignedSubjectCount = allocationCounts?.Count ?? 0,
			SubjectTeacherReadyCount = allocationCounts?.Ready ?? 0,
			DivisionCount = await AsyncExecuter.CountAsync(divisions),
			PublishedTimetableCount = timetableCounts.Where(item => item.Status == TimetableStatus.Published).Sum(item => item.Count),
			ActiveExamCount = examCounts.Sum(item => item.Count),
			ProgressTrackedCount = await AsyncExecuter.CountAsync(progress.Where(item => item.Progress > 0)),
			PublishedTaskCount = await AsyncExecuter.CountAsync(homeworks.Where(homework =>
				homework.Status == HomeworkStatus.Published || homework.IsPublished ||
				homework.DivisionSchedules.Any(schedule => schedule.IsPublished))) +
				await AsyncExecuter.CountAsync(assignments.Where(assignment =>
					assignment.Status == AssignmentStatus.Published || assignment.DivisionSchedules.Any(schedule => schedule.IsPublished))),
			CalendarSettingsCount = await _calendarSettingRepository.CountAsync(),
			ApprovedQuestionCount = questionCounts.Where(item => item.Status == QuestionStatus.Approved).Sum(item => item.Count),
			PublishedOrLockedPaperCount = paperCounts.Where(item => item.Status == QuestionPaperStatus.Published ||
				item.Status == QuestionPaperStatus.Locked || item.IsLocked).Sum(item => item.Count),
			SubmittedDiaryCount = diaries.Where(item => item.Status == TeacherDiaryStatus.Submitted).Sum(item => item.Count),
			ActiveLessonPlanCount = lessonPlans.Where(item => item.Status == LessonPlanStatus.Submitted ||
				item.Status == LessonPlanStatus.Approved || item.Status == LessonPlanStatus.NeedsChanges).Sum(item => item.Count),
			ResultReadyExamCount = await AsyncExecuter.CountAsync(results.Select(result => result.ExamId).Distinct()),
			RoomCount = await _roomRepository.CountAsync(),
			CompleteSeatingPlanCount = seatingCounts.Where(item => item.Status == SeatingPlanStatus.Completed ||
				item.Status == SeatingPlanStatus.Published || item.IsLocked).Sum(item => item.Count),
			SeatAllocationCount = await _seatAllocationRepository.CountAsync()
		};

		return new DashboardSectionSnapshot
		{
			ReadinessItems = BuildReadinessItems(metrics),
			ActionCenterItems = BuildActionCenterItems(pendingTimetables, pendingGradeEntries, pendingEvaluations,
				lessonPlans, diaries, paperCounts, seatingCounts, calendarImpacts),
			DataHealthItems = BuildDataHealthItems(metrics.SubjectCount == 0 || metrics.DivisionCount == 0 || metrics.AssignedSubjectCount == 0,
				pendingTimetables > 0, lessonPlans, diaries, questionCounts, paperCounts, seatingCounts, calendarImpacts),
			PendingEvaluations = pendingEvaluations,
			PendingGradeEntries = pendingGradeEntries,
			UpcomingExams = examCounts.Where(item => item.Status == ExamStatus.Pending).Sum(item => item.Count),
			ReviewedSubmissions = submissionCounts.GetValueOrDefault(SubmissionStatus.Reviewed),
			PendingSubmissions = submissionCounts.GetValueOrDefault(SubmissionStatus.NeedsReview) + submissionCounts.GetValueOrDefault(SubmissionStatus.Submitted),
			TotalSubmissions = submissionCounts.Values.Sum()
		};
	}

	private sealed class DashboardSectionSnapshot
	{
		public List<AcademicDashboardReadinessItemDto> ReadinessItems { get; init; } = [];
		public List<AcademicDashboardActionCenterItemDto> ActionCenterItems { get; init; } = [];
		public List<AcademicDashboardDataHealthItemDto> DataHealthItems { get; init; } = [];
		public int PendingEvaluations { get; init; }
		public int PendingGradeEntries { get; init; }
		public int UpcomingExams { get; init; }
		public int ReviewedSubmissions { get; init; }
		public int PendingSubmissions { get; init; }
		public int TotalSubmissions { get; init; }
	}
}
