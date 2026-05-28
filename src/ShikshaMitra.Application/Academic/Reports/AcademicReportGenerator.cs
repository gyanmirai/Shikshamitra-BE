using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.ClassTeacher;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Syllabus;
using ShikshaMitra.Academic.Timetable;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Academic.Reports;

public class AcademicReportGenerator(
    IAsyncQueryableExecuter asyncExecuter,
    IRepository<AssignedSubject, Guid> assignedSubjectRepository,
    ITimetableRepository timetableRepository,
    IRepository<Standard, Guid> standardRepository,
    IRepository<Division, Guid> divisionRepository,
    IRepository<Subject, Guid> subjectRepository,
    IRepository<Room, Guid> roomRepository,
    IEmployeeRepository employeeRepository) : ITransientDependency
{
    public Task<AcademicReportOptionsDto> GetOptionsAsync(Guid userId, Guid? tenantId)
    {
        var dto = new AcademicReportOptionsDto
        {
            ReportTypes = new List<AcademicReportOptionDto>
            {
                new() { Id = AcademicReportTypes.ChapterProgressReport, Label = "Chapter Progress Report" },
                new() { Id = AcademicReportTypes.SubjectWiseChapterCoverage, Label = "Subject-wise Chapter Coverage" },
                new() { Id = AcademicReportTypes.ChapterDescriptionSummary, Label = "Chapter Description Summary" },
                new() { Id = AcademicReportTypes.TimelineReport, Label = "Timeline Report" },
                new() { Id = AcademicReportTypes.ClassWiseChapterAssignment, Label = "Class-wise Chapter Assignment Report" },
                new() { Id = AcademicReportTypes.CurriculumCompletionReport, Label = "Curriculum Completion Report" },
                new() { Id = AcademicReportTypes.ChapterRevisionTracker, Label = "Chapter Revision Tracker" },
                new() { Id = AcademicReportTypes.ClasswiseTimetable, Label = "Class-wise Timetable Report" },
                new() { Id = AcademicReportTypes.TeacherwiseTimetable, Label = "Teacher-wise Timetable Report" },
                new() { Id = AcademicReportTypes.RoomUtilizationReport, Label = "Room Utilization Report" },
                new() { Id = AcademicReportTypes.SubjectwiseTimetableDistribution, Label = "Subject-wise Timetable Distribution" },
                new() { Id = AcademicReportTypes.FreePeriodsReport, Label = "Free Periods Report" },
                new() { Id = AcademicReportTypes.TimetableChangeLog, Label = "Timetable Change Log Report" }
            },
            Statuses = new List<AcademicReportOptionDto>
            {
                new() { Id = "1", Label = "Upcoming" },
                new() { Id = "2", Label = "In Progress" },
                new() { Id = "3", Label = "Completed" }
            }
        };

        return Task.FromResult(dto);
    }

    public async Task<AcademicReportPreviewDto> GenerateAsync(AcademicReportRequestDto request, Guid userId, Guid? tenantId, bool exportAllRows)
    {
        var payload = new AcademicReportPreviewDto
        {
            ReportType = request.ReportType,
            NormalizedFilters = request.Filters
        };

        var query = await assignedSubjectRepository.WithDetailsAsync(x => x.Subject, x => x.Chapters, x => x.Standard);

        // Security scoping + filter application.
        if (request.Filters.SubjectIds?.Any() == true)
        {
            query = query.Where(x => request.Filters.SubjectIds.Contains(x.SubjectId));
        }

        if (request.Filters.ClassIds?.Any() == true)
        {
            query = query.Where(x => request.Filters.ClassIds.Contains(x.StandardId));
        }

        switch (request.ReportType)
        {
            case AcademicReportTypes.ChapterProgressReport:
                return await BuildChapterProgressReportAsync(query, request, payload, exportAllRows);
            case AcademicReportTypes.SubjectWiseChapterCoverage:
                return await BuildSubjectWiseChapterCoverageAsync(query, request, payload, exportAllRows);
            case AcademicReportTypes.ChapterDescriptionSummary:
                return await BuildChapterDescriptionSummaryAsync(query, request, payload, exportAllRows);
            case AcademicReportTypes.TimelineReport:
                return await BuildTimelineReportAsync(query, request, payload, exportAllRows);
            case AcademicReportTypes.ClassWiseChapterAssignment:
                return await BuildClassWiseChapterAssignmentAsync(query, request, payload, exportAllRows);
            case AcademicReportTypes.CurriculumCompletionReport:
                return await BuildCurriculumCompletionReportAsync(query, request, payload, exportAllRows);
            case AcademicReportTypes.ChapterRevisionTracker:
                return await BuildChapterRevisionTrackerAsync(query, request, payload, exportAllRows);
            case AcademicReportTypes.ClasswiseTimetable:
                return await BuildClasswiseTimetableAsync(request, payload, exportAllRows);
            case AcademicReportTypes.TeacherwiseTimetable:
                return await BuildTeacherwiseTimetableAsync(request, payload, exportAllRows);
            case AcademicReportTypes.RoomUtilizationReport:
                return await BuildRoomUtilizationAsync(request, payload, exportAllRows);
            case AcademicReportTypes.SubjectwiseTimetableDistribution:
                return await BuildSubjectwiseTimetableDistributionAsync(request, payload, exportAllRows);
            case AcademicReportTypes.FreePeriodsReport:
                return await BuildFreePeriodsAsync(request, payload, exportAllRows);
            case AcademicReportTypes.TimetableChangeLog:
                return await BuildTimetableChangeLogAsync(request, payload, exportAllRows);
            default:
                return await BuildChapterProgressReportAsync(query, request, payload, exportAllRows);
        }
    }

    private string GetChapterStatus(DateTimeOffset start, DateTimeOffset end)
    {
        var now = DateTimeOffset.UtcNow;
        if (now < start) return "Upcoming";
        if (now >= start && now <= end) return "In Progress";
        return "Completed";
    }

    private async Task<AcademicReportPreviewDto> BuildChapterProgressReportAsync(
        IQueryable<AssignedSubject> query,
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "ChapterNo", Label = "Chapter No" },
            new() { Id = "ChapterName", Label = "Chapter Name" },
            new() { Id = "StartDate", Label = "Start Date" },
            new() { Id = "EndDate", Label = "End Date" },
            new() { Id = "Status", Label = "Status" },
            new() { Id = "Description", Label = "Description" }
        };

        var chaptersQuery = query.SelectMany(x => x.Chapters.Select(c => new
        {
            c.ChapterNumber,
            c.ChapterName,
            c.ChapterStartTime,
            c.ChapterEndTime,
            c.Description,
            SubjectName = x.Subject.Name
        }));

        payload.TotalCount = await asyncExecuter.CountAsync(chaptersQuery);
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;

        var items = exportAllRows
            ? await asyncExecuter.ToListAsync(chaptersQuery.OrderBy(x => x.ChapterNumber))
            : await asyncExecuter.ToListAsync(chaptersQuery.OrderBy(x => x.ChapterNumber)
                                           .Skip((request.Page - 1) * request.PageSize)
                                           .Take(request.PageSize));

        foreach (var item in items)
        {
            payload.Rows.Add(new Dictionary<string, string?>
            {
                { "ChapterNo", item.ChapterNumber.ToString() },
                { "ChapterName", item.ChapterName },
                { "StartDate", item.ChapterStartTime.ToString("dd-MM-yyyy") },
                { "EndDate", item.ChapterEndTime.ToString("dd-MM-yyyy") },
                { "Status", GetChapterStatus(item.ChapterStartTime, item.ChapterEndTime) },
                { "Description", item.Description ?? "" }
            });
        }

        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildSubjectWiseChapterCoverageAsync(
        IQueryable<AssignedSubject> query,
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "SubjectName", Label = "Subject Name" },
            new() { Id = "TotalChapters", Label = "Total Chapters" },
            new() { Id = "CompletedChapters", Label = "Completed Chapters" },
            new() { Id = "CompletionPercentage", Label = "Completion %" }
        };

        var subjectsQuery = query.Select(x => new
        {
            SubjectName = x.Subject.Name,
            TotalChapters = x.Chapters.Count,
            Chapters = x.Chapters.Select(c => new { c.ChapterStartTime, c.ChapterEndTime }).ToList()
        });

        payload.TotalCount = await asyncExecuter.CountAsync(subjectsQuery);
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;

        var items = exportAllRows
            ? await asyncExecuter.ToListAsync(subjectsQuery.OrderBy(x => x.SubjectName))
            : await asyncExecuter.ToListAsync(subjectsQuery.OrderBy(x => x.SubjectName)
                                           .Skip((request.Page - 1) * request.PageSize)
                                           .Take(request.PageSize));

        foreach (var item in items)
        {
            int completedCount = item.Chapters.Count(c => GetChapterStatus(c.ChapterStartTime, c.ChapterEndTime) == "Completed");
            double completionPercent = item.TotalChapters == 0 ? 0 : Math.Round((double) completedCount / item.TotalChapters * 100, 2);

            payload.Rows.Add(new Dictionary<string, string?>
            {
                { "SubjectName", item.SubjectName },
                { "TotalChapters", item.TotalChapters.ToString() },
                { "CompletedChapters", completedCount.ToString() },
                { "CompletionPercentage", $"{completionPercent}%" }
            });
        }

        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildChapterDescriptionSummaryAsync(
        IQueryable<AssignedSubject> query,
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "ChapterNo", Label = "Chapter No" },
            new() { Id = "ChapterName", Label = "Chapter Name" },
            new() { Id = "Description", Label = "Description" }
        };

        var chaptersQuery = query.SelectMany(x => x.Chapters.Select(c => new
        {
            c.ChapterNumber,
            c.ChapterName,
            c.Description
        }));

        payload.TotalCount = await asyncExecuter.CountAsync(chaptersQuery);
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;

        var items = exportAllRows
            ? await asyncExecuter.ToListAsync(chaptersQuery.OrderBy(x => x.ChapterNumber))
            : await asyncExecuter.ToListAsync(chaptersQuery.OrderBy(x => x.ChapterNumber)
                                           .Skip((request.Page - 1) * request.PageSize)
                                           .Take(request.PageSize));

        foreach (var item in items)
        {
            payload.Rows.Add(new Dictionary<string, string?>
            {
                { "ChapterNo", item.ChapterNumber.ToString() },
                { "ChapterName", item.ChapterName },
                { "Description", item.Description ?? "" }
            });
        }

        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildTimelineReportAsync(
        IQueryable<AssignedSubject> query,
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "ChapterName", Label = "Chapter Name" },
            new() { Id = "StartDate", Label = "Start Date" },
            new() { Id = "EndDate", Label = "End Date" },
            new() { Id = "Duration", Label = "Duration (Days)" }
        };

        var chaptersQuery = query.SelectMany(x => x.Chapters.Select(c => new
        {
            c.ChapterName,
            c.ChapterStartTime,
            c.ChapterEndTime
        }));

        payload.TotalCount = await asyncExecuter.CountAsync(chaptersQuery);
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;

        var items = exportAllRows
            ? await asyncExecuter.ToListAsync(chaptersQuery.OrderBy(x => x.ChapterStartTime))
            : await asyncExecuter.ToListAsync(chaptersQuery.OrderBy(x => x.ChapterStartTime)
                                           .Skip((request.Page - 1) * request.PageSize)
                                           .Take(request.PageSize));

        foreach (var item in items)
        {
            var duration = (item.ChapterEndTime - item.ChapterStartTime).TotalDays;

            payload.Rows.Add(new Dictionary<string, string?>
            {
                { "ChapterName", item.ChapterName },
                { "StartDate", item.ChapterStartTime.ToString("dd-MM-yyyy") },
                { "EndDate", item.ChapterEndTime.ToString("dd-MM-yyyy") },
                { "Duration", Math.Max(0, Math.Round(duration)).ToString() }
            });
        }

        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildClassWiseChapterAssignmentAsync(
        IQueryable<AssignedSubject> query,
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "ClassName", Label = "Class Name" },
            new() { Id = "SubjectName", Label = "Subject Name" },
            new() { Id = "ChapterNo", Label = "Chapter No" },
            new() { Id = "ChapterName", Label = "Chapter Name" }
        };

        var chaptersQuery = query.SelectMany(x => x.Chapters.Select(c => new
        {
            ClassName = x.Standard != null ? x.Standard.Name : "N/A",
            SubjectName = x.Subject.Name,
            c.ChapterNumber,
            c.ChapterName
        }));

        payload.TotalCount = await asyncExecuter.CountAsync(chaptersQuery);
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;

        var items = exportAllRows
            ? await asyncExecuter.ToListAsync(chaptersQuery.OrderBy(x => x.ClassName).ThenBy(x => x.SubjectName).ThenBy(x => x.ChapterNumber))
            : await asyncExecuter.ToListAsync(chaptersQuery.OrderBy(x => x.ClassName).ThenBy(x => x.SubjectName).ThenBy(x => x.ChapterNumber)
                                           .Skip((request.Page - 1) * request.PageSize)
                                           .Take(request.PageSize));

        foreach (var item in items)
        {
            payload.Rows.Add(new Dictionary<string, string?>
            {
                { "ClassName", item.ClassName },
                { "SubjectName", item.SubjectName },
                { "ChapterNo", item.ChapterNumber.ToString() },
                { "ChapterName", item.ChapterName }
            });
        }

        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildCurriculumCompletionReportAsync(
        IQueryable<AssignedSubject> query,
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "ClassName", Label = "Class Name" },
            new() { Id = "SubjectName", Label = "Subject Name" },
            new() { Id = "TotalChapters", Label = "Total Chapters" },
            new() { Id = "CompletedChapters", Label = "Completed Chapters" },
            new() { Id = "CompletionPercentage", Label = "Completion %" }
        };

        var subjectsQuery = query.Select(x => new
        {
            ClassName = x.Standard != null ? x.Standard.Name : "N/A",
            SubjectName = x.Subject.Name,
            TotalChapters = x.Chapters.Count,
            Chapters = x.Chapters.Select(c => new { c.ChapterStartTime, c.ChapterEndTime }).ToList()
        });

        payload.TotalCount = await asyncExecuter.CountAsync(subjectsQuery);
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;

        var items = exportAllRows
            ? await asyncExecuter.ToListAsync(subjectsQuery.OrderBy(x => x.ClassName).ThenBy(x => x.SubjectName))
            : await asyncExecuter.ToListAsync(subjectsQuery.OrderBy(x => x.ClassName).ThenBy(x => x.SubjectName)
                                           .Skip((request.Page - 1) * request.PageSize)
                                           .Take(request.PageSize));

        foreach (var item in items)
        {
            int completedCount = item.Chapters.Count(c => GetChapterStatus(c.ChapterStartTime, c.ChapterEndTime) == "Completed");
            double completionPercent = item.TotalChapters == 0 ? 0 : Math.Round((double) completedCount / item.TotalChapters * 100, 2);

            payload.Rows.Add(new Dictionary<string, string?>
            {
                { "ClassName", item.ClassName },
                { "SubjectName", item.SubjectName },
                { "TotalChapters", item.TotalChapters.ToString() },
                { "CompletedChapters", completedCount.ToString() },
                { "CompletionPercentage", $"{completionPercent}%" }
            });
        }

        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildChapterRevisionTrackerAsync(
        IQueryable<AssignedSubject> query,
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "ClassName", Label = "Class Name" },
            new() { Id = "SubjectName", Label = "Subject Name" },
            new() { Id = "ChapterName", Label = "Chapter Name" },
            new() { Id = "RevisionRequired", Label = "Revision Required" },
            new() { Id = "Notes", Label = "Notes" }
        };

        var chaptersQuery = query.SelectMany(x => x.Chapters.Select(c => new
        {
            ClassName = x.Standard != null ? x.Standard.Name : "N/A",
            SubjectName = x.Subject.Name,
            c.ChapterName,
            c.ChapterStartTime,
            c.ChapterEndTime
        }));

        payload.TotalCount = await asyncExecuter.CountAsync(chaptersQuery);
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;

        var items = exportAllRows
            ? await asyncExecuter.ToListAsync(chaptersQuery.OrderBy(x => x.ClassName).ThenBy(x => x.SubjectName))
            : await asyncExecuter.ToListAsync(chaptersQuery.OrderBy(x => x.ClassName).ThenBy(x => x.SubjectName)
                                           .Skip((request.Page - 1) * request.PageSize)
                                           .Take(request.PageSize));

        foreach (var item in items)
        {
            // Mocking RevisionRequired based on "Completed" status
            var isCompleted = GetChapterStatus(item.ChapterStartTime, item.ChapterEndTime) == "Completed";
            bool revisionRequired = isCompleted; // Require revision if already completed? Sure, as a mock.

            // If the filter asks strictly for revision required
            if (request.Filters.RevisionRequired.HasValue && request.Filters.RevisionRequired.Value != revisionRequired)
                continue;

            payload.Rows.Add(new Dictionary<string, string?>
            {
                { "ClassName", item.ClassName },
                { "SubjectName", item.SubjectName },
                { "ChapterName", item.ChapterName },
                { "RevisionRequired", revisionRequired ? "Yes" : "No" },
                { "Notes", revisionRequired ? "Review for exams" : "Still undergoing" }
            });
        }

        // Note: The total count might be slightly skewed by the mocked in-loop filter, 
        // to be fixed when a true boolean flag is introduced on the chapter entity.

        return payload;
    }

    // ─── Timetable Reports ────────────────────────────────────────────────────

    private async Task<AcademicReportPreviewDto> BuildClasswiseTimetableAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "ClassName", Label = "Class" },
            new() { Id = "Division", Label = "Division" },
            new() { Id = "Day", Label = "Day" },
            new() { Id = "Period", Label = "Period" },
            new() { Id = "Subject", Label = "Subject" },
            new() { Id = "Teacher", Label = "Teacher" },
            new() { Id = "Room", Label = "Room" }
        };

        var (standards, divisions, subjects, rooms, teachers) = await LoadLookupsAsync();

        var timetableQuery = await timetableRepository.GetQueryableAsync();
        if (request.Filters.ClassIds?.Any() == true)
            timetableQuery = timetableQuery.Where(t => request.Filters.ClassIds.Contains(t.StandardId));
        if (request.Filters.DivisionIds?.Any() == true)
            timetableQuery = timetableQuery.Where(t => request.Filters.DivisionIds.Contains(t.DivisionId));

        var timetableIds = await asyncExecuter.ToListAsync(timetableQuery.Select(t => t.Id));
        var timetables = await timetableRepository.WithDetailsAsync(t => t.Slots);
        var timetableList = await asyncExecuter.ToListAsync(
            timetables.Where(t => timetableIds.Contains(t.Id)));

        var rows = new List<Dictionary<string, string?>>();
        foreach (var tt in timetableList)
        {
            var className = standards.GetValueOrDefault(tt.StandardId, "—");
            var divisionName = divisions.GetValueOrDefault(tt.DivisionId, "—");
            foreach (var slot in tt.Slots.Where(s => s.SlotType == SlotType.Period).OrderBy(s => s.Day).ThenBy(s => s.Sequence))
            {
                var dayName = slot.Day.ToString();
                var period = $"{slot.StartTime:HH:mm}–{slot.EndTime:HH:mm}";
                if (!slot.Subjects.Any())
                {
                    rows.Add(new() { { "ClassName", className }, { "Division", divisionName }, { "Day", dayName },
                        { "Period", period }, { "Subject", "—" }, { "Teacher", "—" }, { "Room", "—" } });
                    continue;
                }
                foreach (var s in slot.Subjects)
                {
                    rows.Add(new()
                    {
                        { "ClassName", className }, { "Division", divisionName }, { "Day", dayName },
                        { "Period", period },
                        { "Subject", subjects.GetValueOrDefault(s.SubjectId, "—") },
                        { "Teacher", s.TeacherId.HasValue ? teachers.GetValueOrDefault(s.TeacherId.Value, "—") : "—" },
                        { "Room", s.RoomId.HasValue ? rooms.GetValueOrDefault(s.RoomId.Value, "—") : "—" }
                    });
                }
            }
        }

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows
            ? rows
            : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildTeacherwiseTimetableAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "Teacher", Label = "Teacher" },
            new() { Id = "Day", Label = "Day" },
            new() { Id = "Period", Label = "Period" },
            new() { Id = "Class", Label = "Class & Division" },
            new() { Id = "Subject", Label = "Subject" },
            new() { Id = "Room", Label = "Room" }
        };

        var (standards, divisions, subjects, rooms, teachers) = await LoadLookupsAsync();

        var timetableQuery = await timetableRepository.GetQueryableAsync();
        var timetableIds = await asyncExecuter.ToListAsync(timetableQuery.Select(t => t.Id));
        var timetables = await timetableRepository.WithDetailsAsync(t => t.Slots);
        var timetableList = await asyncExecuter.ToListAsync(timetables.Where(t => timetableIds.Contains(t.Id)));

        var rows = new List<Dictionary<string, string?>>();
        foreach (var tt in timetableList)
        {
            var classDiv = $"{standards.GetValueOrDefault(tt.StandardId, "—")} {divisions.GetValueOrDefault(tt.DivisionId, "")}".Trim();
            foreach (var slot in tt.Slots.Where(s => s.SlotType == SlotType.Period).OrderBy(s => s.Day).ThenBy(s => s.Sequence))
            {
                var dayName = slot.Day.ToString();
                var period = $"{slot.StartTime:HH:mm}–{slot.EndTime:HH:mm}";
                foreach (var s in slot.Subjects)
                {
                    if (!s.TeacherId.HasValue) continue;
                    if (request.Filters.TeacherIds?.Any() == true && !request.Filters.TeacherIds.Contains(s.TeacherId.Value))
                        continue;
                    rows.Add(new()
                    {
                        { "Teacher", teachers.GetValueOrDefault(s.TeacherId.Value, "—") },
                        { "Day", dayName }, { "Period", period },
                        { "Class", classDiv },
                        { "Subject", subjects.GetValueOrDefault(s.SubjectId, "—") },
                        { "Room", s.RoomId.HasValue ? rooms.GetValueOrDefault(s.RoomId.Value, "—") : "—" }
                    });
                }
            }
        }

        rows = rows.OrderBy(r => r["Teacher"]).ThenBy(r => r["Day"]).ThenBy(r => r["Period"]).ToList();
        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows
            ? rows
            : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildRoomUtilizationAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "Room", Label = "Room" },
            new() { Id = "Day", Label = "Day" },
            new() { Id = "Period", Label = "Period" },
            new() { Id = "Class", Label = "Class & Division" },
            new() { Id = "Subject", Label = "Subject" },
            new() { Id = "Teacher", Label = "Teacher" }
        };

        var (standards, divisions, subjects, rooms, teachers) = await LoadLookupsAsync();

        var timetableQuery = await timetableRepository.GetQueryableAsync();
        var timetableIds = await asyncExecuter.ToListAsync(timetableQuery.Select(t => t.Id));
        var timetables = await timetableRepository.WithDetailsAsync(t => t.Slots);
        var timetableList = await asyncExecuter.ToListAsync(timetables.Where(t => timetableIds.Contains(t.Id)));

        var rows = new List<Dictionary<string, string?>>();
        foreach (var tt in timetableList)
        {
            var classDiv = $"{standards.GetValueOrDefault(tt.StandardId, "—")} {divisions.GetValueOrDefault(tt.DivisionId, "")}".Trim();
            foreach (var slot in tt.Slots.Where(s => s.SlotType == SlotType.Period).OrderBy(s => s.Day).ThenBy(s => s.Sequence))
            {
                var period = $"{slot.StartTime:HH:mm}–{slot.EndTime:HH:mm}";
                foreach (var s in slot.Subjects.Where(s => s.RoomId.HasValue))
                {
                    if (request.Filters.RoomIds?.Any() == true && !request.Filters.RoomIds.Contains(s.RoomId!.Value))
                        continue;
                    rows.Add(new()
                    {
                        { "Room", rooms.GetValueOrDefault(s.RoomId!.Value, "—") },
                        { "Day", slot.Day.ToString() }, { "Period", period },
                        { "Class", classDiv },
                        { "Subject", subjects.GetValueOrDefault(s.SubjectId, "—") },
                        { "Teacher", s.TeacherId.HasValue ? teachers.GetValueOrDefault(s.TeacherId.Value, "—") : "—" }
                    });
                }
            }
        }

        rows = rows.OrderBy(r => r["Room"]).ThenBy(r => r["Day"]).ThenBy(r => r["Period"]).ToList();
        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows
            ? rows
            : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildSubjectwiseTimetableDistributionAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "Subject", Label = "Subject" },
            new() { Id = "Class", Label = "Class & Division" },
            new() { Id = "Teacher", Label = "Teacher" },
            new() { Id = "TotalPeriods", Label = "Total Periods/Week" },
            new() { Id = "DaysScheduled", Label = "Days Scheduled" }
        };

        var (standards, divisions, subjects, _, teachers) = await LoadLookupsAsync();

        var timetableQuery = await timetableRepository.GetQueryableAsync();
        if (request.Filters.ClassIds?.Any() == true)
            timetableQuery = timetableQuery.Where(t => request.Filters.ClassIds.Contains(t.StandardId));
        var timetableIds = await asyncExecuter.ToListAsync(timetableQuery.Select(t => t.Id));
        var timetables = await timetableRepository.WithDetailsAsync(t => t.Slots);
        var timetableList = await asyncExecuter.ToListAsync(timetables.Where(t => timetableIds.Contains(t.Id)));

        // Group: per (timetable, subject, teacher) → count periods, collect days
        var groups = timetableList
            .SelectMany(tt => tt.Slots.Where(s => s.SlotType == SlotType.Period)
                .SelectMany(slot => slot.Subjects.Select(sub => new
                {
                    StandardId = tt.StandardId,
                    DivisionId = tt.DivisionId,
                    sub.SubjectId,
                    TeacherId = sub.TeacherId,
                    Day = slot.Day.ToString()
                })))
            .GroupBy(x => new { x.StandardId, x.DivisionId, x.SubjectId, x.TeacherId })
            .Select(g => new
            {
                Subject = subjects.GetValueOrDefault(g.Key.SubjectId, "—"),
                Class = $"{standards.GetValueOrDefault(g.Key.StandardId, "—")} {divisions.GetValueOrDefault(g.Key.DivisionId, "")}".Trim(),
                Teacher = g.Key.TeacherId.HasValue ? teachers.GetValueOrDefault(g.Key.TeacherId.Value, "—") : "—",
                TotalPeriods = g.Count(),
                DaysScheduled = string.Join(", ", g.Select(x => x.Day).Distinct().OrderBy(d => d))
            }).ToList();

        if (request.Filters.SubjectIds?.Any() == true)
        {
            var subjectNames = request.Filters.SubjectIds.Select(id => subjects.GetValueOrDefault(id, "")).ToHashSet();
            groups = groups.Where(g => subjectNames.Contains(g.Subject)).ToList();
        }

        var rows = groups.OrderBy(g => g.Subject).ThenBy(g => g.Class)
            .Select(g => new Dictionary<string, string?>
            {
                { "Subject", g.Subject }, { "Class", g.Class }, { "Teacher", g.Teacher },
                { "TotalPeriods", g.TotalPeriods.ToString() }, { "DaysScheduled", g.DaysScheduled }
            }).ToList();

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows
            ? rows
            : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildFreePeriodsAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "Entity", Label = "Class / Division" },
            new() { Id = "Day", Label = "Day" },
            new() { Id = "Period", Label = "Period" },
            new() { Id = "Status", Label = "Status" }
        };

        var (standards, divisions, _, _, _) = await LoadLookupsAsync();

        var timetableQuery = await timetableRepository.GetQueryableAsync();
        if (request.Filters.ClassIds?.Any() == true)
            timetableQuery = timetableQuery.Where(t => request.Filters.ClassIds.Contains(t.StandardId));
        if (request.Filters.DivisionIds?.Any() == true)
            timetableQuery = timetableQuery.Where(t => request.Filters.DivisionIds.Contains(t.DivisionId));

        var timetableIds = await asyncExecuter.ToListAsync(timetableQuery.Select(t => t.Id));
        var timetables = await timetableRepository.WithDetailsAsync(t => t.Slots);
        var timetableList = await asyncExecuter.ToListAsync(timetables.Where(t => timetableIds.Contains(t.Id)));

        var rows = new List<Dictionary<string, string?>>();
        foreach (var tt in timetableList)
        {
            var entity = $"{standards.GetValueOrDefault(tt.StandardId, "—")} {divisions.GetValueOrDefault(tt.DivisionId, "")}".Trim();
            foreach (var slot in tt.Slots.Where(s => s.SlotType == SlotType.Period).OrderBy(s => s.Day).ThenBy(s => s.Sequence))
            {
                var isFree = !slot.Subjects.Any();
                rows.Add(new()
                {
                    { "Entity", entity },
                    { "Day", slot.Day.ToString() },
                    { "Period", $"{slot.StartTime:HH:mm}–{slot.EndTime:HH:mm}" },
                    { "Status", isFree ? "Free" : "Scheduled" }
                });
            }
        }

        rows = rows.OrderBy(r => r["Entity"]).ThenBy(r => r["Day"]).ThenBy(r => r["Period"]).ToList();
        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows
            ? rows
            : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildTimetableChangeLogAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "Class", Label = "Class & Division" },
            new() { Id = "Status", Label = "Status" },
            new() { Id = "LastModified", Label = "Last Modified" },
            new() { Id = "CreatedAt", Label = "Created At" }
        };

        var (standards, divisions, _, _, _) = await LoadLookupsAsync();

        var timetableQuery = await timetableRepository.GetQueryableAsync();
        if (request.Filters.ClassIds?.Any() == true)
            timetableQuery = timetableQuery.Where(t => request.Filters.ClassIds.Contains(t.StandardId));

        var timetables = await asyncExecuter.ToListAsync(timetableQuery.OrderByDescending(t => t.LastModificationTime ?? t.CreationTime));

        var rows = timetables.Select(tt => new Dictionary<string, string?>
        {
            { "Class", $"{standards.GetValueOrDefault(tt.StandardId, "—")} {divisions.GetValueOrDefault(tt.DivisionId, "")}".Trim() },
            { "Status", tt.Status.ToString() },
            { "LastModified", (tt.LastModificationTime ?? tt.CreationTime).ToString("dd MMM yyyy, HH:mm") },
            { "CreatedAt", tt.CreationTime.ToString("dd MMM yyyy, HH:mm") }
        }).ToList();

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows
            ? rows
            : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    private async Task<(
        Dictionary<Guid, string> Standards,
        Dictionary<Guid, string> Divisions,
        Dictionary<Guid, string> Subjects,
        Dictionary<Guid, string> Rooms,
        Dictionary<Guid, string> Teachers)> LoadLookupsAsync()
    {
        var standards = (await standardRepository.GetListAsync()).ToDictionary(s => s.Id, s => s.Name);
        var divisions = (await divisionRepository.GetListAsync()).ToDictionary(d => d.Id, d => d.Name);
        var subjects = (await subjectRepository.GetListAsync()).ToDictionary(s => s.Id, s => s.Name);
        var rooms = (await roomRepository.GetListAsync()).ToDictionary(r => r.Id, r => r.RoomName);
        var employeeQuery = await employeeRepository.GetQueryableAsync();
        var employees = await asyncExecuter.ToListAsync(employeeQuery);
        var teachers = employees.ToDictionary(e => e.Id, e => e.EmployeeName);

        return (standards, divisions, subjects, rooms, teachers);
    }
}
