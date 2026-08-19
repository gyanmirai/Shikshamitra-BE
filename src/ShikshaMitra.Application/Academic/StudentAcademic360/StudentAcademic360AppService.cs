using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Academic.Examination.Results;
using ShikshaMitra.Academic.Examination.Results.Dtos;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students;
using ShikshaMitra.Students.Attendance;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentEnrollments.Registration;
using ShikshaMitra.Students.StudentRecords;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Entities;
using StandardEntity = ShikshaMitra.TenantSettings.Standards.Standard;
using DivisionEntity = ShikshaMitra.TenantSettings.Divisions.Division;
using AcademicYearEntity = ShikshaMitra.TenantSettings.AcademicYears.AcademicYear;
using EducationMediumEntity = ShikshaMitra.TenantSettings.EducationMediums.EducationMedium;

namespace ShikshaMitra.Academic.StudentAcademic360;

[Authorize]
[RemoteService(false)]
public class StudentAcademic360AppService(
    StudentScopeAccessService studentScopeAccessService,
    IRepository<AttendanceEntry, Guid> attendanceEntryRepository,
    IAttendanceDayRepository attendanceDayRepository,
    ITimetableAppService timetableAppService,
    IRepository<HomeworkAssignmentEntity, Guid> homeworkRepository,
    IRepository<AssignmentEntity, Guid> assignmentRepository,
    IRepository<StudentSubmissionEntity, Guid> submissionRepository,
    IRepository<Exam, Guid> examRepository,
    IRepository<ExamMark, Guid> examMarkRepository,
    IRepository<ExamResultHeader, Guid> resultHeaderRepository,
    IRepository<ExamResultDetail, Guid> resultDetailRepository,
    IRepository<Subject, Guid> subjectRepository,
    IRepository<StandardEntity, Guid> standardRepository,
    IRepository<DivisionEntity, Guid> divisionRepository,
    IRepository<AcademicYearEntity, Guid> academicYearRepository,
    IRepository<EducationMediumEntity, Guid> educationMediumRepository
) : ApplicationService, IStudentAcademic360AppService
{
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Dashboard.View)]
    public async Task<StudentAcademic360Dto> GetAsync(Guid studentId, StudentAcademic360Input input)
    {
        input = NormalizeInput(input);
        var student = await studentScopeAccessService.GetStudentAsync(studentId, nameof(GetAsync), includeDetails: true);
        var academicYearId = await ResolveAcademicYearAsync(student, input);
        var studentDto = await MapStudentAsync(student);
        var attendance = await BuildAttendanceAsync(studentId, input, academicYearId);
        var timetable = await BuildTimetableAsync(student.StandardId, student.DivisionId);
        var learningTasks = await BuildLearningTasksAsync(student, input, academicYearId);
        var exams = await BuildExamResultsAsync(student, input, academicYearId);
        var subjectPerformance = BuildSubjectPerformance(exams);
        var alerts = BuildAlerts(attendance, learningTasks, exams, timetable);
        var academicHealth = BuildAcademicHealth(attendance, learningTasks, exams);
        var upcomingItems = BuildUpcomingItems(timetable, learningTasks, exams);
        var timeline = BuildTimeline(attendance, learningTasks, exams, alerts);
        var achievements = BuildAchievements(attendance, learningTasks, exams, subjectPerformance);
        var dataCompleteness = BuildDataCompleteness(attendance, learningTasks, exams, timetable, subjectPerformance);

        return new StudentAcademic360Dto
        {
            GeneratedAtUtc = Clock.Now.ToUniversalTime(),
            Student = studentDto,
            AcademicHealth = academicHealth,
            Kpis = BuildKpis(attendance, learningTasks, exams, academicHealth),
            Attendance = attendance,
            Timetable = timetable,
            LearningTasks = learningTasks,
            Exams = exams,
            SubjectPerformance = subjectPerformance,
            Alerts = alerts,
            UpcomingItems = upcomingItems,
            Timeline = timeline,
            Achievements = achievements,
            Interventions = [],
            DataCompleteness = dataCompleteness,
            SourceSnapshot = BuildSourceSnapshot(attendance, learningTasks, exams, timetable, subjectPerformance),
            Permissions = new StudentAcademic360PermissionsDto
            {
                CanViewRank = true,
                CanAddObservation = true,
                CanCreateIntervention = true,
                CanDownloadReport = true
            }
        };
    }

    private static StudentAcademic360Input NormalizeInput(StudentAcademic360Input? input)
    {
        input ??= new StudentAcademic360Input();
        input.RecentTaskCount = Math.Clamp(input.RecentTaskCount <= 0 ? 10 : input.RecentTaskCount, 1, 25);
        input.RecentExamCount = Math.Clamp(input.RecentExamCount <= 0 ? 5 : input.RecentExamCount, 1, 10);
        input.TaskSkipCount = Math.Max(0, input.TaskSkipCount);
        input.TaskMaxResultCount = Math.Clamp(input.TaskMaxResultCount <= 0 ? input.RecentTaskCount : input.TaskMaxResultCount, 1, 100);
        input.ExamSkipCount = Math.Max(0, input.ExamSkipCount);
        input.ExamMaxResultCount = Math.Clamp(input.ExamMaxResultCount <= 0 ? input.RecentExamCount : input.ExamMaxResultCount, 1, 50);

        if (input.AttendanceStartDate.HasValue && input.AttendanceEndDate.HasValue)
        {
            var start = input.AttendanceStartDate.Value.Date;
            var end = input.AttendanceEndDate.Value.Date;
            if (start > end)
            {
                (start, end) = (end, start);
            }

            if ((end - start).TotalDays > 370)
            {
                start = end.AddDays(-370);
            }

            input.AttendanceStartDate = start;
            input.AttendanceEndDate = end;
        }

        return input;
    }

    private async Task<Guid?> ResolveAcademicYearAsync(Student student, StudentAcademic360Input input)
    {
        var academicYearId = input.AcademicYearId;
        if (!academicYearId.HasValue && !input.IncludeHistorical)
        {
            academicYearId = student.AcademicYearId;
        }

        if (!academicYearId.HasValue)
        {
            return null;
        }

        if (await academicYearRepository.FindAsync(academicYearId.Value) == null)
        {
            throw new EntityNotFoundException(typeof(AcademicYearEntity), academicYearId.Value);
        }

        await studentScopeAccessService.EnsureCanAccessAcademicYearAsync(academicYearId.Value, nameof(GetAsync));
        return academicYearId;
    }

    private async Task<StudentDto> MapStudentAsync(Student student)
    {
        var standardName = student.StandardId.HasValue
            ? (await standardRepository.FindAsync(student.StandardId.Value))?.Name ?? string.Empty
            : string.Empty;
        var divisionName = student.DivisionId.HasValue
            ? (await divisionRepository.FindAsync(student.DivisionId.Value))?.Name ?? string.Empty
            : string.Empty;
        var academicYearName = student.AcademicYearId.HasValue
            ? (await academicYearRepository.FindAsync(student.AcademicYearId.Value))?.Name
            : null;
        var mediumName = (await educationMediumRepository.FindAsync(student.AcademicMediumId))?.Name;

        return new StudentDto
        {
            StudentId = student.Id,
            InquiryNo = student.AdditionalDetail?.InquiryNo ?? string.Empty,
            FormNo = student.AdditionalDetail?.RegistrationFormNo,
            StudentName = BuildStudentName(student.FirstName, student.MiddleName, student.LastName),
            FirstName = student.FirstName,
            MiddleName = student.MiddleName,
            LastName = student.LastName,
            AcademicYearId = student.AcademicYearId,
            AcademicYearName = academicYearName,
            AcademicBoardId = student.AcademicBoardId,
            AcademicMediumId = student.AcademicMediumId,
            AcademicMediumName = mediumName,
            StandardId = student.StandardId ?? Guid.Empty,
            Standard = standardName,
            DivisionId = student.DivisionId,
            Division = divisionName,
            DateOfBirth = student.AdditionalDetail?.DateOfBirth,
            DateOfInquiry = student.AdditionalDetail?.DateOfInquiry,
            Gender = student.AdditionalDetail?.Gender,
            RollNo = student.RollNo,
            GrNo = student.GrNo,
            Category = student.AdditionalDetail?.Category ?? Category.General,
            IsLibraryMember = student.AdditionalDetail?.IsLibraryMember == true,
            IsTransportMember = student.AdditionalDetail?.IsTransportMember == true,
            Address1 = student.ContactDetail?.Address1,
            Address2 = student.ContactDetail?.Address2,
            Address3 = student.ContactDetail?.Address3,
            City = student.ContactDetail?.City,
            State = student.ContactDetail?.State,
            Country = student.ContactDetail?.Country,
            PinCode = student.ContactDetail?.Pincode,
            PrimaryContactNo = student.ContactDetail?.PrimaryContactNo,
            SecondaryContactNo = student.ContactDetail?.SecondaryContactNo,
            Email = student.ContactDetail?.Email,
            PreviousSchool = student.AdditionalDetail?.PreviousSchool,
            InquiryPreferenceId = student.AdditionalDetail?.InquiryPreferenceId,
            IsFeeStructureAssigned = student.FeeStructureId.HasValue,
            StudentEnrollmentState = student.Status,
            Status = student.Status,
            OperationalStatus = student.Status == StudentLifeCycleStatusEnum.Left
                ? StudentStatus.Left
                : student.Status == StudentLifeCycleStatusEnum.InActive
                    ? StudentStatus.Inactive
                    : StudentStatus.Active,
            HasPhoto = !string.IsNullOrWhiteSpace(student.PhotoBlobName),
            PhotoUrl = !string.IsNullOrWhiteSpace(student.PhotoBlobName)
                ? $"/api/students/records/{student.Id}/photo"
                : null
        };
    }

    private async Task<StudentAcademic360AttendanceDto> BuildAttendanceAsync(
        Guid studentId,
        StudentAcademic360Input input,
        Guid? academicYearId)
    {
        var (startDate, endDate) = ResolveAttendanceDateRange(input.AttendanceStartDate, input.AttendanceEndDate);
        var scopedDays = await studentScopeAccessService.ApplyCurrentUserAttendanceDayScopeAsync(
            await attendanceDayRepository.GetQueryableAsync());
        var entries = await attendanceEntryRepository.GetQueryableAsync();

        var rows = await AsyncExecuter.ToListAsync(
            from entry in entries
            join day in scopedDays on entry.AttendanceDayId equals day.Id
            where entry.StudentId == studentId && day.Date >= startDate && day.Date <= endDate
                && (!academicYearId.HasValue || day.AcademicYearId == academicYearId.Value)
            orderby day.Date descending
            select new StudentAttendanceHistoryDto
            {
                AttendanceDayId = entry.AttendanceDayId,
                Date = day.Date,
                Status = entry.Status,
                Remarks = entry.Remarks
            });

        var counted = rows.Where(x => x.Status is AttendanceStatus.Present or AttendanceStatus.Absent).ToList();
        var present = counted.Count(x => x.Status == AttendanceStatus.Present);
        var absent = counted.Count(x => x.Status == AttendanceStatus.Absent);

        return new StudentAcademic360AttendanceDto
        {
            TotalDays = counted.Count,
            PresentDays = present,
            AbsentDays = absent,
            PendingDays = rows.Count(x => x.Status == AttendanceStatus.Pending),
            Percentage = counted.Count == 0 ? 0 : Math.Round(present * 100d / counted.Count, 2),
            RecentHistory = rows.Take(30).ToList()
        };
    }

    private async Task<TimetableResponseDto> BuildTimetableAsync(Guid? standardId, Guid? divisionId)
    {
        if (!standardId.HasValue || !divisionId.HasValue)
        {
            return new TimetableResponseDto();
        }

        try
        {
            return await timetableAppService.GetByStandardAndDivisionAsync(standardId.Value, divisionId.Value)
                ?? new TimetableResponseDto();
        }
        catch (BusinessException)
        {
            return new TimetableResponseDto();
        }
    }

    private async Task<StudentAcademic360LearningTaskSummaryDto> BuildLearningTasksAsync(
        Student student,
        StudentAcademic360Input input,
        Guid? academicYearId)
    {
        if (!student.StandardId.HasValue || !student.DivisionId.HasValue)
        {
            return new StudentAcademic360LearningTaskSummaryDto();
        }

        var divisionId = student.DivisionId.Value;
        var submissions = await submissionRepository.GetListAsync(s => s.StudentId == student.Id);
        var submissionByHomework = submissions
            .Where(s => s.HomeworkAssignmentId.HasValue)
            .GroupBy(s => s.HomeworkAssignmentId!.Value)
            .ToDictionary(g => g.Key, g => g
                .OrderByDescending(x => x.SubmissionDate ?? DateTimeOffset.MinValue)
                .ThenByDescending(x => x.CreationTime)
                .First());
        var submissionByAssignment = submissions
            .Where(s => s.AssignmentId.HasValue)
            .GroupBy(s => s.AssignmentId!.Value)
            .ToDictionary(g => g.Key, g => g
                .OrderByDescending(x => x.SubmissionDate ?? DateTimeOffset.MinValue)
                .ThenByDescending(x => x.CreationTime)
                .First());

        var homeworkQuery = await homeworkRepository.WithDetailsAsync(h => h.DivisionSchedules);
        var homeworks = await AsyncExecuter.ToListAsync(homeworkQuery.Where(h =>
            h.StandardId == student.StandardId.Value &&
            (!academicYearId.HasValue || h.AcademicYearId == academicYearId.Value) &&
            (h.Status == HomeworkStatus.Published || h.IsPublished || h.DivisionSchedules.Any(s => s.IsPublished)) &&
            ((h.DivisionId == divisionId && (h.IsPublished || h.Status == HomeworkStatus.Published)) ||
             h.DivisionSchedules.Any(s => s.DivisionId == divisionId && s.IsPublished))));

        var assignmentQuery = await assignmentRepository.WithDetailsAsync(a => a.DivisionSchedules);
        var assignments = await AsyncExecuter.ToListAsync(assignmentQuery.Where(a =>
            a.StandardId == student.StandardId.Value &&
            (!academicYearId.HasValue || a.AcademicYearId == academicYearId.Value) &&
            (a.Status == AssignmentStatus.Published || a.DivisionSchedules.Any(s => s.IsPublished)) &&
            a.DivisionSchedules.Any(s => s.DivisionId == divisionId && s.IsPublished)));

        var subjectNames = await LoadSubjectNamesAsync(
            homeworks.Select(h => h.SubjectId).Concat(assignments.Select(a => a.SubjectId)));
        var tasks = new List<StudentAcademic360LearningTaskDto>();
        tasks.AddRange(homeworks.Select(homework =>
        {
            var schedule = homework.DivisionSchedules.FirstOrDefault(s => s.DivisionId == divisionId);
            submissionByHomework.TryGetValue(homework.Id, out var submission);
            var status = ResolveSubmissionStatus(submission, schedule?.DueDate ?? homework.DueDate);
            return new StudentAcademic360LearningTaskDto
            {
                Id = homework.Id,
                Type = "Homework",
                Title = homework.Title,
                SubjectId = homework.SubjectId,
                SubjectName = subjectNames.GetValueOrDefault(homework.SubjectId, "Subject"),
                DueDate = schedule?.DueDate ?? homework.DueDate ?? homework.CreationTime,
                Status = status.ToString(),
                StatusCode = (int)status,
                Marks = submission?.Marks,
                TotalMarks = submission?.TotalMarks,
                Feedback = submission?.Feedback
            };
        }));

        tasks.AddRange(assignments.Select(assignment =>
        {
            var schedule = assignment.DivisionSchedules.FirstOrDefault(s => s.DivisionId == divisionId && s.IsPublished);
            if (schedule == null)
            {
                return null;
            }

            submissionByAssignment.TryGetValue(assignment.Id, out var submission);
            var status = ResolveSubmissionStatus(submission, schedule.DueDate);
            return new StudentAcademic360LearningTaskDto
            {
                Id = assignment.Id,
                Type = "Assignment",
                Title = assignment.Title,
                SubjectId = assignment.SubjectId,
                SubjectName = subjectNames.GetValueOrDefault(assignment.SubjectId, "Subject"),
                DueDate = schedule.DueDate,
                Status = status.ToString(),
                StatusCode = (int)status,
                Marks = submission?.Marks,
                TotalMarks = submission?.TotalMarks ?? assignment.TotalMarks,
                Feedback = submission?.Feedback
            };
        }).Where(task => task != null)!);

        var ordered = tasks.OrderByDescending(t => t.DueDate).ToList();

        return new StudentAcademic360LearningTaskSummaryDto
        {
            TotalAssigned = ordered.Count,
            Submitted = ordered.Count(t => IsSubmitted((SubmissionStatus)t.StatusCode)),
            Pending = ordered.Count(t => t.StatusCode is (int)SubmissionStatus.NotSubmitted or (int)SubmissionStatus.Draft),
            Late = ordered.Count(t => t.StatusCode == (int)SubmissionStatus.Late),
            Reviewed = ordered.Count(t => t.StatusCode == (int)SubmissionStatus.Reviewed),
            PendingEvaluation = ordered.Count(t => t.StatusCode is (int)SubmissionStatus.Submitted or (int)SubmissionStatus.NeedsReview or (int)SubmissionStatus.Late),
            RecentTasks = ordered
                .Skip(input.TaskSkipCount)
                .Take(input.TaskMaxResultCount)
                .ToList()
        };
    }

    private async Task<List<StudentAcademic360ExamResultDto>> BuildExamResultsAsync(
        Student student,
        StudentAcademic360Input input,
        Guid? academicYearId)
    {
        if (!student.StandardId.HasValue || !student.DivisionId.HasValue)
        {
            return [];
        }

        var divisionId = student.DivisionId.Value;
        var examQuery = await examRepository.WithDetailsAsync(e => e.ExamSubjects, e => e.ExamDivisions);
        var scopedExams = await AsyncExecuter.ToListAsync(examQuery
            .Where(e => e.StandardId == student.StandardId.Value
                && e.ExamDivisions.Any(d => d.DivisionId == divisionId))
            .OrderByDescending(e => e.CreationTime));

        if (scopedExams.Count == 0)
        {
            return [];
        }

        var allExamIds = scopedExams.Select(e => e.Id).ToList();
        var publishedMarkExamIds = (await examMarkRepository.GetListAsync(m =>
                allExamIds.Contains(m.ExamId) &&
                m.StudentId == student.Id &&
                m.DivisionId == divisionId &&
                m.IsPublished))
            .Select(m => m.ExamId)
            .ToHashSet();
        var headers = await resultHeaderRepository.GetListAsync(h =>
            allExamIds.Contains(h.ExamId) &&
            h.DivisionId == divisionId &&
            (h.Status == ExamResultStatus.Published || h.Status == ExamResultStatus.Frozen));
        var publishedResultExamIds = headers.Select(h => h.ExamId).ToHashSet();
        var exams = scopedExams
            .Where(e =>
                e.Status is ExamStatus.Published or ExamStatus.Completed ||
                publishedResultExamIds.Contains(e.Id) ||
                publishedMarkExamIds.Contains(e.Id))
            .Skip(input.ExamSkipCount)
            .Take(input.ExamMaxResultCount)
            .ToList();

        if (exams.Count == 0)
        {
            return [];
        }

        var examIds = exams.Select(e => e.Id).ToList();
        var subjectNames = await LoadSubjectNamesAsync(exams.SelectMany(e => e.ExamSubjects.Select(s => s.SubjectId)));
        var marks = await examMarkRepository.GetListAsync(m =>
            examIds.Contains(m.ExamId) &&
            m.StudentId == student.Id &&
            m.DivisionId == divisionId);
        headers = headers.Where(h => examIds.Contains(h.ExamId)).ToList();
        var headerIds = headers.Select(h => h.Id).ToList();
        var details = headerIds.Count == 0
            ? []
            : await resultDetailRepository.GetListAsync(d =>
                headerIds.Contains(d.ResultHeaderId) &&
                d.StudentId == student.Id &&
                d.DivisionId == divisionId);

        return exams.Select(exam =>
        {
            var header = headers.FirstOrDefault(h => h.ExamId == exam.Id);
            var detail = header == null ? null : details.FirstOrDefault(d => d.ResultHeaderId == header.Id);
            var examMarks = marks.Where(m => m.ExamId == exam.Id).ToList();
            var totalMarks = detail?.TotalMaxMarks ?? exam.ExamSubjects.Sum(s => s.TotalMarks ?? 0);
            var obtained = detail?.TotalObtainedMarks ?? examMarks.Where(m => !m.IsAbsent).Sum(m => m.MarksObtained ?? 0);
            var percentage = detail?.Percentage ?? (totalMarks <= 0 ? 0 : Math.Round(obtained * 100d / totalMarks, 2));

            return new StudentAcademic360ExamResultDto
            {
                ExamId = exam.Id,
                ExamName = exam.ExamName,
                ResultStatus = detail?.ResultStatus.ToString() ?? header?.Status.ToString() ?? exam.Status.ToString(),
                Rank = detail?.ClassRank,
                TotalMarks = totalMarks,
                ObtainedMarks = obtained,
                Percentage = percentage,
                Grade = detail?.Grade ?? string.Empty,
                Subjects = exam.ExamSubjects.Select(subject =>
                {
                    var mark = examMarks.FirstOrDefault(m => m.SubjectId == subject.SubjectId);
                    return new StudentSubjectMarkDto
                    {
                        SubjectName = subjectNames.GetValueOrDefault(subject.SubjectId, "Subject"),
                        MaxMarks = (subject.TotalMarks ?? 0).ToString("F2"),
                        MarksObtained = mark == null ? string.Empty : mark.IsAbsent ? "Ab" : (mark.MarksObtained ?? 0).ToString("F2"),
                        Grade = string.Empty,
                        Remarks = mark?.Feedback ?? string.Empty,
                        IsPassed = mark != null && !mark.IsAbsent && (mark.MarksObtained ?? 0) >= (subject.PassingMarks ?? 0)
                    };
                }).ToList()
            };
        }).ToList();
    }

    private static List<StudentAcademic360SubjectPerformanceDto> BuildSubjectPerformance(
        IReadOnlyCollection<StudentAcademic360ExamResultDto> exams)
    {
        return exams
            .SelectMany(e => e.Subjects.Select(subject => new
            {
                subject.SubjectName,
                Marks = double.TryParse(subject.MarksObtained, out var obtained) ? obtained : (double?)null,
                Max = double.TryParse(subject.MaxMarks, out var max) ? max : (double?)null,
                subject.Grade
            }))
            .Where(x => x.Marks.HasValue && x.Max.HasValue && x.Max.Value > 0)
            .GroupBy(x => x.SubjectName)
            .Select(group => new StudentAcademic360SubjectPerformanceDto
            {
                SubjectName = group.Key,
                ExamCount = group.Count(),
                AveragePercentage = Math.Round(group.Average(x => x.Marks!.Value * 100d / x.Max!.Value), 2),
                LatestGrade = group.FirstOrDefault()?.Grade ?? string.Empty
            })
            .OrderBy(x => x.SubjectName)
            .ToList();
    }

    private static List<StudentAcademic360AlertDto> BuildAlerts(
        StudentAcademic360AttendanceDto attendance,
        StudentAcademic360LearningTaskSummaryDto tasks,
        IReadOnlyCollection<StudentAcademic360ExamResultDto> exams,
        TimetableResponseDto timetable)
    {
        var alerts = new List<StudentAcademic360AlertDto>();
        if (attendance.TotalDays > 0 && attendance.Percentage < 75)
        {
            alerts.Add(new StudentAcademic360AlertDto
            {
                Severity = "Attention",
                Title = "Low attendance",
                Message = $"Attendance is {attendance.Percentage}%.",
                Source = "Attendance",
                Evidence = $"{attendance.PresentDays}/{attendance.TotalDays} attendance days present.",
                SuggestedAction = "Review recent absences and plan a follow-up with the class teacher."
            });
        }

        if (tasks.Pending > 0 || tasks.Late > 0)
        {
            alerts.Add(new StudentAcademic360AlertDto
            {
                Severity = tasks.Late > 0 ? "Important" : "Attention",
                Title = "Learning tasks pending",
                Message = $"{tasks.Pending} pending and {tasks.Late} late tasks found.",
                Source = "HomeworkAssignment",
                Evidence = $"{tasks.TotalAssigned} total homework/assignment records.",
                SuggestedAction = tasks.Late > 0 ? "Send a reminder or create a short completion plan." : "Review pending tasks with the student."
            });
        }

        if (exams.Any(e => e.ResultStatus.Contains("Fail", StringComparison.OrdinalIgnoreCase)))
        {
            alerts.Add(new StudentAcademic360AlertDto
            {
                Severity = "Urgent",
                Title = "Exam result needs attention",
                Message = "One or more exam results are marked as failed.",
                Source = "Examination",
                Evidence = "At least one result status contains Fail.",
                SuggestedAction = "Review subject marks and create a remedial support plan."
            });
        }

        if (timetable == null || timetable.TotalSlots == 0)
        {
            alerts.Add(new StudentAcademic360AlertDto
            {
                Severity = "Information",
                Title = "Timetable not available",
                Message = "No timetable is configured for this student's class and division.",
                Source = "Timetable",
                Evidence = "No timetable slots were returned for the student's class/division.",
                SuggestedAction = "Publish the class timetable to show upcoming lectures."
            });
        }

        return alerts;
    }

    private static StudentAcademic360HealthDto? BuildAcademicHealth(
        StudentAcademic360AttendanceDto attendance,
        StudentAcademic360LearningTaskSummaryDto tasks,
        IReadOnlyCollection<StudentAcademic360ExamResultDto> exams)
    {
        if (attendance.TotalDays == 0 && tasks.TotalAssigned == 0 && exams.Count == 0)
        {
            return new StudentAcademic360HealthDto
            {
                Score = null,
                Status = "Insufficient Data",
                Trend = "Not enough data",
                Explanation = "Attendance, learning task, or examination records are required before calculating a reliable academic health score."
            };
        }

        var attendanceEarned = attendance.TotalDays == 0 ? 0 : (int)Math.Round(attendance.Percentage * 25 / 100);
        var homeworkRate = tasks.TotalAssigned == 0 ? 0 : tasks.Submitted * 100d / tasks.TotalAssigned;
        var homeworkEarned = tasks.TotalAssigned == 0 ? 0 : (int)Math.Round(homeworkRate * 25 / 100);
        var examAverage = exams.Count == 0 ? 0 : exams.Average(e => e.Percentage);
        var examEarned = exams.Count == 0 ? 0 : (int)Math.Round(examAverage * 30 / 100);
        var consistencyEarned = tasks.Late == 0 && attendance.AbsentDays <= 2 ? 10 : 5;
        var improvementEarned = exams.Count >= 2 && exams.First().Percentage >= exams.Last().Percentage ? 10 : exams.Count > 0 ? 6 : 0;
        var score = attendanceEarned + homeworkEarned + examEarned + consistencyEarned + improvementEarned;

        return new StudentAcademic360HealthDto
        {
            Score = score,
            Status = score >= 80 ? "On Track" : score >= 65 ? "Improving" : score >= 50 ? "Needs Attention" : "Support Recommended",
            Trend = exams.Count >= 2 && exams.First().Percentage >= exams.Last().Percentage ? "Improving" : "Monitoring",
            Explanation = "Weighted from attendance, homework completion, examination average, consistency, and improvement.",
            Factors =
            [
                new() { Label = "Attendance", Earned = attendanceEarned, Maximum = 25, Evidence = attendance.TotalDays == 0 ? "No attendance data" : $"{attendance.Percentage}% attendance" },
                new() { Label = "Homework", Earned = homeworkEarned, Maximum = 25, Evidence = tasks.TotalAssigned == 0 ? "No learning tasks" : $"{tasks.Submitted}/{tasks.TotalAssigned} submitted" },
                new() { Label = "Examinations", Earned = examEarned, Maximum = 30, Evidence = exams.Count == 0 ? "No exam data" : $"{Math.Round(examAverage, 2)}% average" },
                new() { Label = "Consistency", Earned = consistencyEarned, Maximum = 10, Evidence = $"{tasks.Late} late tasks, {attendance.AbsentDays} absences" },
                new() { Label = "Improvement", Earned = improvementEarned, Maximum = 10, Evidence = exams.Count < 2 ? "Need at least two exams" : "Compared latest and previous exams" }
            ]
        };
    }

    private static List<StudentAcademic360KpiDto> BuildKpis(
        StudentAcademic360AttendanceDto attendance,
        StudentAcademic360LearningTaskSummaryDto tasks,
        IReadOnlyCollection<StudentAcademic360ExamResultDto> exams,
        StudentAcademic360HealthDto? health)
    {
        var homeworkRate = tasks.TotalAssigned == 0 ? (double?)null : Math.Round(tasks.Submitted * 100d / tasks.TotalAssigned, 2);
        var examAverage = exams.Count == 0 ? (double?)null : Math.Round(exams.Average(e => e.Percentage), 2);

        return
        [
            new() { Key = "attendance", Label = "Attendance", DisplayValue = attendance.TotalDays == 0 ? "No data" : $"{attendance.Percentage}%", Status = attendance.TotalDays == 0 ? "no-data" : attendance.Percentage >= 75 ? "good" : "attention", Trend = "Last 30 days", DataSource = "Student attendance", Tooltip = "Present days divided by marked attendance days." },
            new() { Key = "homework", Label = "Homework Completion", DisplayValue = homeworkRate.HasValue ? $"{homeworkRate}%" : "No data", Status = homeworkRate.HasValue ? homeworkRate >= 80 ? "good" : "attention" : "no-data", Trend = $"{tasks.Pending} pending", DataSource = "Homework and assignment submissions", Tooltip = "Submitted tasks divided by assigned tasks." },
            new() { Key = "examAverage", Label = "Exam Average", DisplayValue = examAverage.HasValue ? $"{examAverage}%" : "No data", Status = examAverage.HasValue ? examAverage >= 60 ? "good" : "attention" : "no-data", Trend = $"{exams.Count} exams", DataSource = "Exam marks and results", Tooltip = "Average percentage across recent exams." },
            new() { Key = "health", Label = "Academic Health", DisplayValue = health?.Score.HasValue == true ? $"{health.Score}/100" : "Insufficient data", Status = health?.Status ?? "no-data", Trend = health?.Trend ?? "Not enough data", DataSource = "Academic 360 summary", Tooltip = health?.Explanation ?? "Requires attendance, homework, or exam data." }
        ];
    }

    private static List<StudentAcademic360UpcomingItemDto> BuildUpcomingItems(
        TimetableResponseDto timetable,
        StudentAcademic360LearningTaskSummaryDto tasks,
        IReadOnlyCollection<StudentAcademic360ExamResultDto> exams)
    {
        var today = DateTime.UtcNow.Date;
        var items = tasks.RecentTasks
            .Where(t => t.DueDate.Date >= today.AddDays(-7))
            .Select(t => new StudentAcademic360UpcomingItemDto
            {
                Date = t.DueDate.Date,
                Title = $"{t.Type}: {t.Title}",
                Type = t.Type,
                Status = t.Status,
                IsOverdue = t.DueDate.Date < today && t.StatusCode is (int)SubmissionStatus.NotSubmitted or (int)SubmissionStatus.Late
            })
            .ToList();

        if (timetable is { TotalSlots: > 0 })
        {
            items.Add(new StudentAcademic360UpcomingItemDto
            {
                Date = today,
                Title = $"{timetable.OccupiedSlots}/{timetable.TotalSlots} timetable slots configured",
                Type = "Timetable",
                Status = "Available"
            });
        }

        items.AddRange(exams.Take(3).Select(e => new StudentAcademic360UpcomingItemDto
        {
            Date = today,
            Title = e.ExamName,
            Type = "Exam",
            Status = e.ResultStatus
        }));

        return items.OrderBy(i => i.Date).ThenBy(i => i.Time).Take(12).ToList();
    }

    private static List<StudentAcademic360TimelineEventDto> BuildTimeline(
        StudentAcademic360AttendanceDto attendance,
        StudentAcademic360LearningTaskSummaryDto tasks,
        IReadOnlyCollection<StudentAcademic360ExamResultDto> exams,
        IReadOnlyCollection<StudentAcademic360AlertDto> alerts)
    {
        var events = new List<StudentAcademic360TimelineEventDto>();
        events.AddRange(attendance.RecentHistory.Take(10).Select(a => new StudentAcademic360TimelineEventDto
        {
            Date = a.Date,
            Type = "Attendance",
            Title = $"Attendance marked {a.Status}",
            Description = a.Remarks ?? "Attendance record",
            Severity = a.Status == AttendanceStatus.Absent ? "Attention" : "Information",
            Source = "Attendance"
        }));
        events.AddRange(tasks.RecentTasks.Take(10).Select(t => new StudentAcademic360TimelineEventDto
        {
            Date = t.DueDate,
            Type = t.Type,
            Title = t.Title,
            Description = $"{t.SubjectName} | {t.Status}",
            Severity = t.StatusCode == (int)SubmissionStatus.Late ? "Important" : "Information",
            Source = "Learning"
        }));
        events.AddRange(exams.Take(10).Select(e => new StudentAcademic360TimelineEventDto
        {
            Date = DateTime.UtcNow.Date,
            Type = "Exam",
            Title = e.ExamName,
            Description = e.Percentage > 0 ? $"{e.Percentage}% | {e.ResultStatus}" : e.ResultStatus,
            Severity = e.ResultStatus.Contains("Fail", StringComparison.OrdinalIgnoreCase) ? "Important" : "Information",
            Source = "Examination"
        }));
        events.AddRange(alerts.Select(a => new StudentAcademic360TimelineEventDto
        {
            Date = a.Date ?? DateTime.UtcNow.Date,
            Type = "Alert",
            Title = a.Title,
            Description = a.Message,
            Severity = a.Severity,
            Source = a.Source
        }));

        return events.OrderByDescending(e => e.Date).Take(30).ToList();
    }

    private static List<StudentAcademic360AchievementDto> BuildAchievements(
        StudentAcademic360AttendanceDto attendance,
        StudentAcademic360LearningTaskSummaryDto tasks,
        IReadOnlyCollection<StudentAcademic360ExamResultDto> exams,
        IReadOnlyCollection<StudentAcademic360SubjectPerformanceDto> subjects)
    {
        var achievements = new List<StudentAcademic360AchievementDto>();
        if (attendance.TotalDays > 0 && attendance.Percentage >= 90)
        {
            achievements.Add(new() { Title = "Strong attendance", Description = $"{attendance.Percentage}% attendance in the selected period.", Source = "Attendance" });
        }
        if (tasks.TotalAssigned > 0 && tasks.Late == 0)
        {
            achievements.Add(new() { Title = "On-time learning tasks", Description = "No late homework or assignments found.", Source = "Learning" });
        }
        var bestSubject = subjects.OrderByDescending(s => s.AveragePercentage).FirstOrDefault();
        if (bestSubject != null)
        {
            achievements.Add(new() { Title = $"Strong in {bestSubject.SubjectName}", Description = $"{bestSubject.AveragePercentage}% average across {bestSubject.ExamCount} assessments.", Source = "Examination" });
        }
        if (exams.Any(e => e.Percentage >= 85))
        {
            achievements.Add(new() { Title = "High exam performance", Description = "At least one recent exam score is 85% or above.", Source = "Examination" });
        }
        return achievements;
    }

    private static StudentAcademic360DataCompletenessDto BuildDataCompleteness(
        StudentAcademic360AttendanceDto attendance,
        StudentAcademic360LearningTaskSummaryDto tasks,
        IReadOnlyCollection<StudentAcademic360ExamResultDto> exams,
        TimetableResponseDto timetable,
        IReadOnlyCollection<StudentAcademic360SubjectPerformanceDto> subjects)
    {
        var available = new List<string>();
        var missing = new List<string>();
        AddCompleteness(attendance.TotalDays > 0, "Attendance history", available, missing);
        AddCompleteness(tasks.TotalAssigned > 0, "Homework and assignment data", available, missing);
        AddCompleteness(exams.Count > 0, "Exam result data", available, missing);
        AddCompleteness(timetable is { TotalSlots: > 0 }, "Published timetable", available, missing);
        AddCompleteness(subjects.Count > 0, "Subject performance", available, missing);
        var percentage = (int)Math.Round(available.Count * 100d / (available.Count + missing.Count));

        return new StudentAcademic360DataCompletenessDto
        {
            Percentage = percentage,
            Status = percentage == 100 ? "Complete" : percentage >= 60 ? "Partial data" : "Limited data",
            AvailableItems = available,
            MissingItems = missing
        };
    }

    private static List<StudentAcademic360SourceSnapshotDto> BuildSourceSnapshot(
        StudentAcademic360AttendanceDto attendance,
        StudentAcademic360LearningTaskSummaryDto tasks,
        IReadOnlyCollection<StudentAcademic360ExamResultDto> exams,
        TimetableResponseDto timetable,
        IReadOnlyCollection<StudentAcademic360SubjectPerformanceDto> subjects)
    {
        return
        [
            new()
            {
                Source = "Attendance",
                Count = attendance.TotalDays,
                Status = attendance.TotalDays > 0 ? "Available" : "No data",
                LastUpdatedAtUtc = attendance.RecentHistory.FirstOrDefault()?.Date
            },
            new()
            {
                Source = "HomeworkAssignment",
                Count = tasks.TotalAssigned,
                Status = tasks.TotalAssigned > 0 ? "Available" : "No data",
                LastUpdatedAtUtc = tasks.RecentTasks.OrderByDescending(x => x.DueDate).FirstOrDefault()?.DueDate
            },
            new()
            {
                Source = "Examination",
                Count = exams.Count,
                Status = exams.Count > 0 ? "Available" : "No data"
            },
            new()
            {
                Source = "Timetable",
                Count = timetable.TotalSlots,
                Status = timetable.TotalSlots > 0 ? "Available" : "No data"
            },
            new()
            {
                Source = "SubjectPerformance",
                Count = subjects.Count,
                Status = subjects.Count > 0 ? "Available" : "No data"
            }
        ];
    }

    private static void AddCompleteness(bool condition, string label, ICollection<string> available, ICollection<string> missing)
    {
        if (condition)
        {
            available.Add(label);
        }
        else
        {
            missing.Add(label);
        }
    }

    private async Task<Dictionary<Guid, string>> LoadSubjectNamesAsync(IEnumerable<Guid> subjectIds)
    {
        var ids = subjectIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        var subjects = await subjectRepository.GetListAsync(s => ids.Contains(s.Id));
        return subjects.ToDictionary(s => s.Id, s => string.IsNullOrWhiteSpace(s.DisplayName) ? s.Name : s.DisplayName!);
    }

    private (DateTime StartDate, DateTime EndDate) ResolveAttendanceDateRange(DateTime? startDate, DateTime? endDate)
    {
        var now = Clock.Now.Date;
        var defaultStart = now.AddDays(-30);
        var start = (startDate ?? defaultStart).Date;
        var end = (endDate ?? now).Date;
        return start <= end ? (start, end) : (end, start);
    }

    private static SubmissionStatus ResolveSubmissionStatus(StudentSubmissionEntity? submission, DateTime? dueDate)
    {
        if (submission != null)
        {
            return submission.Status;
        }

        if (dueDate.HasValue && dueDate.Value.Date < DateTime.UtcNow.Date)
        {
            return SubmissionStatus.Late;
        }

        return SubmissionStatus.NotSubmitted;
    }

    private static bool IsSubmitted(SubmissionStatus status) =>
        status is SubmissionStatus.Submitted or SubmissionStatus.Late or SubmissionStatus.Reviewed
            or SubmissionStatus.Excused or SubmissionStatus.NeedsReview;

    private static string BuildStudentName(string? firstName, string? middleName, string? lastName) =>
        string.Join(" ", new[] { firstName, middleName, lastName }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
}
