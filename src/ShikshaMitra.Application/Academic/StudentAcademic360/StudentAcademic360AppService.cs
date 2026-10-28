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
public partial class StudentAcademic360AppService(
    StudentScopeAccessService studentScopeAccessService,
    IStudentRepository studentRepository,
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
        var student = await GetAcademicStudentAsync(studentId, nameof(GetAsync));
        var academicYearId = await ResolveAcademicYearAsync(student, input);
        var studentDto = await MapStudentAsync(student);
        var attendance = await BuildAttendanceAsync(studentId, input, academicYearId);
        var timetable = await BuildTimetableAsync(student.StandardId, student.DivisionId);
        var learningTasks = await BuildLearningTasksAsync(student, input, academicYearId);
        var exams = await BuildExamResultsAsync(student, input, academicYearId);
        var allExams = input.ExamMaxResultCount > 0 || input.ExamSkipCount > 0
            ? await LoadExamSummaryAsync(student) : exams;
        var subjectPerformance = BuildSubjectPerformance(allExams);
        var alerts = BuildAlerts(attendance, learningTasks, allExams, timetable);
        var academicHealth = BuildAcademicHealth(attendance, learningTasks, allExams);
        var upcomingItems = BuildUpcomingItems(timetable, learningTasks, allExams);
        var timeline = BuildTimeline(attendance, learningTasks, allExams, alerts);
        var achievements = BuildAchievements(attendance, learningTasks, allExams, subjectPerformance);
        var dataCompleteness = BuildDataCompleteness(attendance, learningTasks, allExams, timetable, subjectPerformance);

        return new StudentAcademic360Dto
        {
            GeneratedAtUtc = Clock.Now.ToUniversalTime(),
            Student = studentDto,
            AcademicHealth = academicHealth,
            Kpis = BuildKpis(attendance, learningTasks, allExams, academicHealth),
            Attendance = attendance,
            Timetable = timetable,
            LearningTasks = learningTasks,
            Exams = exams,
            ExamTotalCount = allExams.Count,
            ExamSummary = BuildExamSummary(allExams),
            SubjectPerformance = subjectPerformance,
            Alerts = alerts,
            UpcomingItems = upcomingItems,
            Timeline = timeline,
            Achievements = achievements,
            Interventions = [],
            DataCompleteness = dataCompleteness,
            SourceSnapshot = BuildSourceSnapshot(attendance, learningTasks, allExams, timetable, subjectPerformance),
            Permissions = new StudentAcademic360PermissionsDto
            {
                CanViewRank = true,
                CanAddObservation = true,
                CanCreateIntervention = true,
                CanDownloadReport = true
            }
        };
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Dashboard.View)]
    public async Task<StudentAcademic360Dto> GetTabAsync(Guid studentId, string tab, StudentAcademic360Input input)
    {
        input = NormalizeInput(input);
        var student = await GetAcademicStudentAsync(studentId, nameof(GetTabAsync));
        var academicYearId = await ResolveAcademicYearAsync(student, input);
        var response = new StudentAcademic360Dto
        {
            GeneratedAtUtc = Clock.Now.ToUniversalTime(),
            Student = await MapStudentAsync(student)
        };

        switch (tab.Trim().ToLowerInvariant())
        {
            case "overview":
                input.TaskSkipCount = 0;
                input.TaskMaxResultCount = input.RecentTaskCount;
                input.ExamSkipCount = 0;
                input.ExamMaxResultCount = input.RecentExamCount;
                return await GetAsync(studentId, input);
            case "attendance":
                response.Attendance = await BuildAttendanceAsync(studentId, input, academicYearId);
                break;
            case "learning":
                input.TaskMaxResultCount = Math.Clamp(input.TaskMaxResultCount <= 0 ? 20 : input.TaskMaxResultCount, 1, 100);
                response.LearningTasks = await BuildLearningTasksAsync(student, input, academicYearId);
                break;
            case "exams":
                input.ExamMaxResultCount = Math.Clamp(input.ExamMaxResultCount <= 0 ? 10 : input.ExamMaxResultCount, 1, 100);
                var allExams = await LoadExamSummaryAsync(student);
                var filteredExams = FilterDetailExams(allExams, input);
                if (input.DetailPaging) filteredExams = filteredExams.OrderByDescending(exam => exam.ExamDate ?? DateTime.MinValue);
                var matchingExams = filteredExams.ToList();
                var examIds = matchingExams.Skip(input.ExamSkipCount).Take(input.ExamMaxResultCount).Select(exam => exam.ExamId).ToList();
                var examDetails = await BuildExamResultsAsync(student, input, academicYearId, examIds);
                var detailsById = examDetails.ToDictionary(exam => exam.ExamId);
                response.Exams = examIds.Where(detailsById.ContainsKey).Select(id => detailsById[id]).ToList();
                response.ExamTotalCount = matchingExams.Count;
                response.ExamSummary = BuildExamSummary(allExams);
                response.SubjectPerformance = BuildSubjectPerformance(allExams);
                break;
            case "timetable":
                response.Timetable = await BuildTimetableAsync(student.StandardId, student.DivisionId);
                break;
            case "timeline":
                input.TaskSkipCount = 0;
                input.TaskMaxResultCount = input.RecentTaskCount;
                var attendance = await BuildAttendanceAsync(studentId, input, academicYearId);
                var learningTasks = await BuildLearningTasksAsync(student, input, academicYearId);
                var exams = await LoadExamSummaryAsync(student);
                var timetable = await BuildTimetableAsync(student.StandardId, student.DivisionId);
                response.Timeline = BuildTimeline(attendance, learningTasks, exams, BuildAlerts(attendance, learningTasks, exams, timetable));
                break;
            default:
                throw new UserFriendlyException("Unsupported Student Academic 360 tab.");
        }

        return response;
    }

    private static StudentAcademic360Input NormalizeInput(StudentAcademic360Input? input)
    {
        input ??= new StudentAcademic360Input();
        input.RecentTaskCount = Math.Clamp(input.RecentTaskCount <= 0 ? 10 : input.RecentTaskCount, 1, 25);
        input.RecentExamCount = Math.Clamp(input.RecentExamCount <= 0 ? 5 : input.RecentExamCount, 1, 10);
        input.TaskSkipCount = Math.Max(0, input.TaskSkipCount);
        input.TaskMaxResultCount = input.TaskMaxResultCount < 0 ? 0 : Math.Min(input.TaskMaxResultCount, 10000);
        input.ExamSkipCount = Math.Max(0, input.ExamSkipCount);
        input.ExamMaxResultCount = input.ExamMaxResultCount < 0 ? 0 : Math.Min(input.ExamMaxResultCount, 10000);

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

        if (!await academicYearRepository.AnyAsync(year => year.Id == academicYearId.Value))
        {
            throw new EntityNotFoundException(typeof(AcademicYearEntity), academicYearId.Value);
        }

        await studentScopeAccessService.EnsureCanAccessAcademicYearAsync(academicYearId.Value, nameof(GetAsync));
        return academicYearId;
    }

    private async Task<StudentDto> MapStudentAsync(Student student)
    {
        var standardName = student.StandardId.HasValue
            ? await AsyncExecuter.FirstOrDefaultAsync((await standardRepository.GetQueryableAsync())
                .Where(standard => standard.Id == student.StandardId.Value).Select(standard => standard.Name)) ?? string.Empty
            : string.Empty;
        var divisionName = student.DivisionId.HasValue
            ? await AsyncExecuter.FirstOrDefaultAsync((await divisionRepository.GetQueryableAsync())
                .Where(division => division.Id == student.DivisionId.Value).Select(division => division.Name)) ?? string.Empty
            : string.Empty;
        var academicYearName = student.AcademicYearId.HasValue
            ? await AsyncExecuter.FirstOrDefaultAsync((await academicYearRepository.GetQueryableAsync())
                .Where(year => year.Id == student.AcademicYearId.Value).Select(year => year.Name))
            : null;
        var mediumName = await AsyncExecuter.FirstOrDefaultAsync((await educationMediumRepository.GetQueryableAsync())
            .Where(medium => medium.Id == student.AcademicMediumId).Select(medium => medium.Name));

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

    private async Task<Student> GetAcademicStudentAsync(Guid studentId, string operation)
    {
        // Academic 360 consumes the same two detail sections as registration. The
        // repository explicitly suppresses the setup hierarchy's automatic includes.
        var student = await studentRepository.GetRegistrationAggregateAsync(studentId)
            ?? throw new EntityNotFoundException(typeof(Student), studentId);
        await studentScopeAccessService.EnsureCanAccessStudentAsync(student, operation);
        return student;
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

        var historyQuery =
            from entry in entries
            join day in scopedDays on entry.AttendanceDayId equals day.Id
            where entry.StudentId == studentId && day.Date >= startDate && day.Date <= endDate
                && (!academicYearId.HasValue || day.AcademicYearId == academicYearId.Value)
            select new StudentAttendanceHistoryDto
            {
                AttendanceDayId = entry.AttendanceDayId,
                Date = day.Date,
                Status = entry.Status,
                Remarks = entry.Remarks
            };

        var statusCounts = await AsyncExecuter.ToListAsync(historyQuery.GroupBy(row => row.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() }));
        var counts = statusCounts.ToDictionary(group => group.Status, group => group.Count);
        var present = counts.GetValueOrDefault(AttendanceStatus.Present);
        var absent = counts.GetValueOrDefault(AttendanceStatus.Absent);
        var counted = present + absent;
        var recent = await AsyncExecuter.ToListAsync(historyQuery.OrderByDescending(row => row.Date).Take(30));

        return new StudentAcademic360AttendanceDto
        {
            TotalDays = counted,
            PresentDays = present,
            AbsentDays = absent,
            PendingDays = counts.GetValueOrDefault(AttendanceStatus.Pending),
            Percentage = counted == 0 ? 0 : Math.Round(present * 100d / counted, 2),
            RecentHistory = recent
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
        var homeworkQuery = await homeworkRepository.GetQueryableAsync();
        var homeworks = await AsyncExecuter.ToListAsync(homeworkQuery.Where(h =>
            h.StandardId == student.StandardId.Value &&
            (!academicYearId.HasValue || h.AcademicYearId == academicYearId.Value) &&
            (h.Status == HomeworkStatus.Published || h.IsPublished || h.DivisionSchedules.Any(s => s.IsPublished)) &&
            ((h.DivisionId == divisionId && (h.IsPublished || h.Status == HomeworkStatus.Published)) ||
             h.DivisionSchedules.Any(s => s.DivisionId == divisionId && s.IsPublished)))
            .OrderBy(h => h.Id)
            .Select(h => new LearningTaskRead
            {
                Id = h.Id, Title = h.Title, SubjectId = h.SubjectId, CreationTime = h.CreationTime,
                AssignedDate = h.DivisionSchedules.Where(schedule => schedule.DivisionId == divisionId)
                    .Select(schedule => schedule.PublishedAt ?? schedule.PublishDate).FirstOrDefault() ?? h.CreationTime,
                DueDate = h.DivisionSchedules.Where(schedule => schedule.DivisionId == divisionId)
                    .Select(schedule => (DateTime?)schedule.DueDate).FirstOrDefault() ?? h.DueDate
            }));

        var assignmentQuery = await assignmentRepository.GetQueryableAsync();
        var assignments = await AsyncExecuter.ToListAsync(assignmentQuery.Where(a =>
            a.StandardId == student.StandardId.Value &&
            (!academicYearId.HasValue || a.AcademicYearId == academicYearId.Value) &&
            (a.Status == AssignmentStatus.Published || a.DivisionSchedules.Any(s => s.IsPublished)) &&
            a.DivisionSchedules.Any(s => s.DivisionId == divisionId && s.IsPublished))
            .OrderBy(a => a.Id)
            .Select(a => new LearningTaskRead
            {
                Id = a.Id, Title = a.Title, SubjectId = a.SubjectId, CreationTime = a.CreationTime, TotalMarks = a.TotalMarks,
                AssignedDate = a.DivisionSchedules.Where(schedule => schedule.DivisionId == divisionId && schedule.IsPublished)
                    .Select(schedule => schedule.PublishedAt ?? schedule.PublishDate).FirstOrDefault() ?? a.CreationTime,
                DueDate = a.DivisionSchedules.Where(schedule => schedule.DivisionId == divisionId && schedule.IsPublished)
                    .Select(schedule => (DateTime?)schedule.DueDate).FirstOrDefault()
            }));

        var homeworkIds = homeworks.Select(homework => homework.Id).ToList();
        var assignmentIds = assignments.Select(assignment => assignment.Id).ToList();
        var submissionQuery = await submissionRepository.GetQueryableAsync();
        var submissions = await AsyncExecuter.ToListAsync(submissionQuery.Where(submission => submission.StudentId == student.Id &&
                ((submission.HomeworkAssignmentId.HasValue && homeworkIds.Contains(submission.HomeworkAssignmentId.Value)) ||
                 (submission.AssignmentId.HasValue && assignmentIds.Contains(submission.AssignmentId.Value))))
            .Select(submission => new SubmissionRead
            {
                Id = submission.Id,
                HomeworkAssignmentId = submission.HomeworkAssignmentId, AssignmentId = submission.AssignmentId,
                Status = submission.Status, SubmissionDate = submission.SubmissionDate, CreationTime = submission.CreationTime,
                ReviewedAt = submission.ReviewedAt, GradedDate = submission.GradedDate,
                Marks = submission.Marks, TotalMarks = submission.TotalMarks
            }));
        var submissionByHomework = submissions.Where(submission => submission.HomeworkAssignmentId.HasValue)
            .GroupBy(submission => submission.HomeworkAssignmentId!.Value)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(submission => submission.SubmissionDate ?? DateTimeOffset.MinValue)
                .ThenByDescending(submission => submission.CreationTime).First());
        var submissionByAssignment = submissions.Where(submission => submission.AssignmentId.HasValue)
            .GroupBy(submission => submission.AssignmentId!.Value)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(submission => submission.SubmissionDate ?? DateTimeOffset.MinValue)
                .ThenByDescending(submission => submission.CreationTime).First());

        var subjectNames = await LoadSubjectNamesAsync(
            homeworks.Select(h => h.SubjectId).Concat(assignments.Select(a => a.SubjectId)));
        var tasks = new List<StudentAcademic360LearningTaskDto>();
        tasks.AddRange(homeworks.Select(homework =>
        {
            submissionByHomework.TryGetValue(homework.Id, out var submission);
            var status = ResolveSubmissionStatus(submission, homework.DueDate);
            return new StudentAcademic360LearningTaskDto
            {
                Id = homework.Id,
                Type = "Homework",
                Title = homework.Title,
                SubjectId = homework.SubjectId,
                SubjectName = subjectNames.GetValueOrDefault(homework.SubjectId, "Subject"),
                AssignedDate = homework.AssignedDate,
                DueDate = homework.DueDate ?? homework.CreationTime,
                Status = status.ToString(),
                SubmissionStatus = status.ToString(),
                EvaluationStatus = ResolveEvaluationStatus(submission),
                SubmissionDate = submission?.SubmissionDate?.DateTime,
                EvaluationDate = submission?.ReviewedAt?.DateTime ?? submission?.GradedDate?.DateTime,
                StatusCode = (int)status,
                Marks = submission?.Marks,
                TotalMarks = submission?.TotalMarks,
                Feedback = submission?.Feedback
            };
        }));

        tasks.AddRange(assignments.Select(assignment =>
        {
            submissionByAssignment.TryGetValue(assignment.Id, out var submission);
            var status = ResolveSubmissionStatus(submission, assignment.DueDate);
            return new StudentAcademic360LearningTaskDto
            {
                Id = assignment.Id,
                Type = "Assignment",
                Title = assignment.Title,
                SubjectId = assignment.SubjectId,
                SubjectName = subjectNames.GetValueOrDefault(assignment.SubjectId, "Subject"),
                AssignedDate = assignment.AssignedDate,
                DueDate = assignment.DueDate ?? assignment.CreationTime,
                Status = status.ToString(),
                SubmissionStatus = status.ToString(),
                EvaluationStatus = ResolveEvaluationStatus(submission),
                SubmissionDate = submission?.SubmissionDate?.DateTime,
                EvaluationDate = submission?.ReviewedAt?.DateTime ?? submission?.GradedDate?.DateTime,
                StatusCode = (int)status,
                Marks = submission?.Marks,
                TotalMarks = submission?.TotalMarks ?? assignment.TotalMarks,
                Feedback = submission?.Feedback
            };
        }));

        var ordered = tasks.OrderByDescending(t => t.DueDate).ToList();
        var filtered = ordered.Where(task =>
            (string.IsNullOrEmpty(input.TaskStatusFilter) || input.TaskStatusFilter == "ALL" ||
             ResolveTaskFilterCategory(task) == input.TaskStatusFilter) &&
            (string.IsNullOrEmpty(input.TaskSubjectName) || input.TaskSubjectName == "ALL" ||
             task.SubjectName == input.TaskSubjectName)).ToList();
        var requestedTasks = input.TaskMaxResultCount <= 0
            ? filtered.Skip(input.TaskSkipCount).ToList()
            : filtered.Skip(input.TaskSkipCount).Take(input.TaskMaxResultCount).ToList();
        var recentTasks = ordered.Take(input.RecentTaskCount).ToList();
        var selectedTaskIds = requestedTasks.Concat(recentTasks).Select(task => task.Id).ToHashSet();
        var selectedSubmissions = submissionByHomework.Where(pair => selectedTaskIds.Contains(pair.Key)).Select(pair => pair.Value)
            .Concat(submissionByAssignment.Where(pair => selectedTaskIds.Contains(pair.Key)).Select(pair => pair.Value)).ToList();
        var selectedSubmissionIds = selectedSubmissions.Select(submission => submission.Id).ToArray();
        var feedback = await AsyncExecuter.ToListAsync(submissionQuery.Where(submission =>
            Enumerable.Contains(selectedSubmissionIds, submission.Id)).Select(submission => new { submission.Id, submission.Feedback }));
        var feedbackById = feedback.ToDictionary(row => row.Id, row => row.Feedback);
        foreach (var task in requestedTasks.Concat(recentTasks))
        {
            var submissionsByTask = task.Type == "Assignment" ? submissionByAssignment : submissionByHomework;
            if (submissionsByTask.TryGetValue(task.Id, out var submission)) task.Feedback = feedbackById.GetValueOrDefault(submission.Id);
        }

        return new StudentAcademic360LearningTaskSummaryDto
        {
            TotalAssigned = ordered.Count,
            FilteredTotalCount = filtered.Count,
            SubjectNames = ordered.Select(task => task.SubjectName).Distinct().ToList(),
            ResubmissionRequired = ordered.Count(task => ResolveTaskFilterCategory(task) == "RESUBMISSION_REQUIRED"),
            Submitted = ordered.Count(t => IsSubmitted((SubmissionStatus)t.StatusCode)),
            Pending = ordered.Count(t => t.StatusCode is (int)SubmissionStatus.NotSubmitted or (int)SubmissionStatus.Draft),
            Late = ordered.Count(t => t.StatusCode == (int)SubmissionStatus.Late),
            Reviewed = ordered.Count(t => t.StatusCode == (int)SubmissionStatus.Reviewed),
            PendingEvaluation = ordered.Count(t => t.StatusCode is (int)SubmissionStatus.Submitted or (int)SubmissionStatus.NeedsReview or (int)SubmissionStatus.Late),
            Tasks = requestedTasks,
            RecentTasks = recentTasks
        };
    }

    private async Task<List<StudentAcademic360ExamResultDto>> BuildExamResultsAsync(
        Student student,
        StudentAcademic360Input input,
        Guid? academicYearId,
        IReadOnlyCollection<Guid>? selectedExamIds = null)
    {
        if (!student.StandardId.HasValue || !student.DivisionId.HasValue)
        {
            return [];
        }

        var divisionId = student.DivisionId.Value;
        var examQuery = await examRepository.WithDetailsAsync(e => e.ExamSubjects, e => e.ExamDivisions);
        var publishedHeaders = (await resultHeaderRepository.GetQueryableAsync()).Where(header =>
            header.DivisionId == divisionId &&
            (header.Status == ExamResultStatus.Published || header.Status == ExamResultStatus.Frozen));
        var publishedMarks = (await examMarkRepository.GetQueryableAsync()).Where(mark =>
            mark.StudentId == student.Id && mark.DivisionId == divisionId && mark.IsPublished);
        var visibleQuery = examQuery
            .Where(e => e.StandardId == student.StandardId.Value
                && e.ExamDivisions.Any(d => d.DivisionId == divisionId)
                && (e.Status == ExamStatus.Published || e.Status == ExamStatus.Completed ||
                    publishedHeaders.Any(header => header.ExamId == e.Id) ||
                    publishedMarks.Any(mark => mark.ExamId == e.Id)));
        if (selectedExamIds != null) visibleQuery = visibleQuery.Where(exam => selectedExamIds.Contains(exam.Id));
        var visibleExams = visibleQuery.OrderByDescending(e => e.CreationTime).Skip(selectedExamIds == null ? input.ExamSkipCount : 0);

        if (selectedExamIds == null && input.ExamMaxResultCount > 0)
        {
            visibleExams = visibleExams.Take(input.ExamMaxResultCount);
        }

        var exams = await AsyncExecuter.ToListAsync(visibleExams);
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
        var headers = await AsyncExecuter.ToListAsync(publishedHeaders.Where(header => examIds.Contains(header.ExamId)));
        var headerIds = headers.Select(h => h.Id).ToList();
        var details = headerIds.Count == 0
            ? []
            : await resultDetailRepository.GetListAsync(d =>
                headerIds.Contains(d.ResultHeaderId) &&
                d.StudentId == student.Id &&
                d.DivisionId == divisionId);

        var headersByExam = headers.ToLookup(header => header.ExamId);
        var detailsByHeader = details.ToLookup(detail => detail.ResultHeaderId);
        var marksByExam = marks.ToLookup(mark => mark.ExamId);
        return exams.Select(exam =>
        {
            var header = headersByExam[exam.Id].FirstOrDefault();
            var detail = header == null ? null : detailsByHeader[header.Id].FirstOrDefault();
            var examMarks = marksByExam[exam.Id].ToList();
            var totalMarks = detail?.TotalMaxMarks ?? exam.ExamSubjects.Sum(s => s.TotalMarks ?? 0);
            var obtained = detail?.TotalObtainedMarks ?? examMarks.Where(m => !m.IsAbsent).Sum(m => m.MarksObtained ?? 0);
            var percentage = detail?.Percentage ?? (totalMarks <= 0 ? 0 : Math.Round(obtained * 100d / totalMarks, 2));

            var examStart = exam.ExamSubjects.Where(s => s.StartTime.HasValue).Select(s => s.StartTime!.Value).OrderBy(x => x).FirstOrDefault();
            var examEnd = exam.ExamSubjects.Where(s => s.EndTime.HasValue).Select(s => s.EndTime!.Value).OrderByDescending(x => x).FirstOrDefault();
            var examDate = exam.ExamSubjects.Where(s => s.ExamDate.HasValue).Select(s => s.ExamDate!.Value).OrderBy(x => x).FirstOrDefault();
            var examStatus = ResolveExamStatus(exam.Status, examDate, examStart, examEnd);
            var gradingStatus = ResolveGradingStatus(examStatus, header?.Status);

            return new StudentAcademic360ExamResultDto
            {
                ExamId = exam.Id,
                ExamName = exam.ExamName,
                ExamStatus = examStatus,
                GradingStatus = gradingStatus,
                ExamDate = examDate == default ? null : examDate,
                StartDate = examStart == default ? null : examStart,
                EndDate = examEnd == default ? null : examEnd,
                ResultAvailable = header?.Status is ExamResultStatus.Published or ExamResultStatus.Frozen,
                ResultPublishedDate = header?.PublishedOn,
                ResultStatus = gradingStatus,
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

    private static string ResolveExamStatus(
        ExamStatus persistedStatus,
        DateTime? examDate,
        DateTime? startTime,
        DateTime? endTime)
    {
        if (persistedStatus == ExamStatus.Cancelled)
        {
            return "Cancelled";
        }

        if (persistedStatus == ExamStatus.Completed)
        {
            return "Completed";
        }

        if (!examDate.HasValue)
        {
            return persistedStatus == ExamStatus.Ongoing ? "Ongoing" : "Scheduled";
        }

        var start = examDate.Value.Date.Add(startTime?.TimeOfDay ?? TimeSpan.Zero);
        var end = examDate.Value.Date.Add(endTime?.TimeOfDay ?? TimeSpan.FromDays(1).Subtract(TimeSpan.FromTicks(1)));
        var now = DateTime.Now;
        if (now >= end)
        {
            return "Completed";
        }

        if (now >= start)
        {
            return "Ongoing";
        }

        return "Scheduled";
    }

    private static string ResolveGradingStatus(string examStatus, ExamResultStatus? resultStatus)
    {
        if (examStatus is "Scheduled" or "Ongoing")
        {
            return "Not Applicable";
        }

        return resultStatus switch
        {
            ExamResultStatus.Generated => "In Progress",
            ExamResultStatus.Published or ExamResultStatus.Frozen => "Completed",
            _ => "Not Started"
        };
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

    private static string ResolveEvaluationStatus(SubmissionRead? submission)
    {
        if (submission == null || submission.Status is SubmissionStatus.NotSubmitted or SubmissionStatus.Draft)
        {
            return "Not Started";
        }

        return submission.Status == SubmissionStatus.Reviewed || submission.Marks.HasValue
            ? "Completed"
            : "In Progress";
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

        var subjectQuery = await subjectRepository.GetQueryableAsync();
        var subjects = await AsyncExecuter.ToListAsync(subjectQuery.Where(subject => ids.Contains(subject.Id))
            .Select(subject => new { subject.Id, subject.Name, subject.DisplayName }));
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

    private static SubmissionStatus ResolveSubmissionStatus(SubmissionRead? submission, DateTime? dueDate)
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

    private sealed class LearningTaskRead
    {
        public Guid Id { get; init; }
        public Guid SubjectId { get; init; }
        public string Title { get; init; } = string.Empty;
        public DateTime CreationTime { get; init; }
        public DateTime AssignedDate { get; init; }
        public DateTime? DueDate { get; init; }
        public decimal? TotalMarks { get; init; }
    }

    private sealed class SubmissionRead
    {
        public Guid Id { get; init; }
        public Guid? HomeworkAssignmentId { get; init; }
        public Guid? AssignmentId { get; init; }
        public SubmissionStatus Status { get; init; }
        public DateTimeOffset? SubmissionDate { get; init; }
        public DateTime CreationTime { get; init; }
        public DateTimeOffset? ReviewedAt { get; init; }
        public DateTimeOffset? GradedDate { get; init; }
        public decimal? Marks { get; init; }
        public decimal? TotalMarks { get; init; }
        public string? Feedback { get; init; }
    }
}
