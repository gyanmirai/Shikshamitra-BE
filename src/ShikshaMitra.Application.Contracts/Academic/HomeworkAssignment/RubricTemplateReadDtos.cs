using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public class GetRubricTemplatePageInput : GetRubricsInput
{
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 10;
    public string? Filters { get; set; }
    public string? Sorts { get; set; }
    public string? TimeZoneId { get; set; }
}

public class RubricTemplateSummaryDto
{
    public Guid Id { get; set; }
    public Guid? AcademicMediumId { get; set; }
    public string Title { get; set; } = string.Empty;
    public WorkType? AppliesToWorkType { get; set; }
    public RubricScoringModel ScoringModel { get; set; }
    public RubricVisibility Visibility { get; set; }
    public decimal TotalMarks { get; set; }
    public int Version { get; set; }
    public RubricStatus Status { get; set; }
    public DateTime CreationTime { get; set; }
    public DateTime? LastModificationTime { get; set; }
    public int CriteriaCount { get; set; }
    public int UsageCount { get; set; }
    public bool HasPerformanceLevels { get; set; }
}

public class RubricTemplateCountsDto
{
    public int Total { get; set; }
    public int Published { get; set; }
    public int Draft { get; set; }
    public int Archived { get; set; }
    public int Linked { get; set; }
}

public class RubricTemplatePageDto
{
    public List<RubricTemplateSummaryDto> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public RubricTemplateCountsDto Summary { get; set; } = new();
}

[RemoteService(IsEnabled = false)]
public interface IRubricTemplateReadAppService : IApplicationService
{
    Task<RubricTemplatePageDto> GetPageAsync(GetRubricTemplatePageInput input);
}
