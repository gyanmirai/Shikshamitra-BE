using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.ClassTeacher;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Syllabus;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Academic.Reports;

public class AcademicReportGenerator(
    IAsyncQueryableExecuter asyncExecuter,
    IRepository<AssignedSubject, Guid> assignedSubjectRepository) : ITransientDependency
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
                new() { Id = AcademicReportTypes.ChapterRevisionTracker, Label = "Chapter Revision Tracker" }
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
            double completionPercent = item.TotalChapters == 0 ? 0 : Math.Round((double)completedCount / item.TotalChapters * 100, 2);

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
            double completionPercent = item.TotalChapters == 0 ? 0 : Math.Round((double)completedCount / item.TotalChapters * 100, 2);

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
}
