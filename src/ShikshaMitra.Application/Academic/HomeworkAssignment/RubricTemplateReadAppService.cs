using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.HomeworkAssignment;

[Authorize(ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_SettingsRubricConfig.View)]
[RemoteService(IsEnabled = false)]
public class RubricTemplateReadAppService(IRepository<AssessmentRubric, Guid> rubricRepository,
    IRepository<AssessmentRubricCriterion, Guid> criterionRepository) : ApplicationService, IRubricTemplateReadAppService
{
    public async Task<RubricTemplatePageDto> GetPageAsync(GetRubricTemplatePageInput input)
    {
        var all = await rubricRepository.GetQueryableAsync();
        var criteria = await criterionRepository.GetQueryableAsync();
        var query = all.Where(r => r.IsTemplate &&
            (!input.StandardId.HasValue || r.StandardId == input.StandardId.Value) &&
            (!input.SubjectId.HasValue || r.SubjectId == input.SubjectId.Value) &&
            (!input.AcademicMediumId.HasValue || !r.AcademicMediumId.HasValue || r.AcademicMediumId == input.AcademicMediumId.Value) &&
            (!input.HomeworkId.HasValue || r.HomeworkId == input.HomeworkId.Value) &&
            (!input.AssignmentId.HasValue || r.AssignmentId == input.AssignmentId.Value) &&
            (!input.HomeworkTypeId.HasValue || !r.HomeworkTypeId.HasValue || r.HomeworkTypeId == input.HomeworkTypeId.Value) &&
            (!input.AssignmentTypeId.HasValue || !r.AssignmentTypeId.HasValue || r.AssignmentTypeId == input.AssignmentTypeId.Value) &&
            (!input.AppliesToWorkType.HasValue || !r.AppliesToWorkType.HasValue ||
                r.AppliesToWorkType == input.AppliesToWorkType.Value || r.AppliesToWorkType == WorkType.Both) &&
            (!input.Status.HasValue || r.Status == input.Status.Value));
        var keys = await AsyncExecuter.ToListAsync(query.Select(rubric => new RubricTemplateSummaryDto
        {
            Id = rubric.Id, AcademicMediumId = rubric.AcademicMediumId, Title = rubric.Title,
            AppliesToWorkType = rubric.AppliesToWorkType, ScoringModel = rubric.ScoringModel, Visibility = rubric.Visibility,
            TotalMarks = rubric.TotalMarks, Version = rubric.Version, Status = rubric.Status,
            CreationTime = rubric.CreationTime, LastModificationTime = rubric.LastModificationTime,
            CriteriaCount = criteria.Count(criterion => criterion.RubricId == rubric.Id),
            UsageCount = all.Count(linked => !linked.IsTemplate && linked.SourceTemplateId == rubric.Id &&
                (!input.AcademicMediumId.HasValue || !linked.AcademicMediumId.HasValue || linked.AcademicMediumId == input.AcademicMediumId.Value)),
            HasPerformanceLevels = rubric.PerformanceLevelsJson != null &&
                rubric.PerformanceLevelsJson.Replace(" ", "").Replace("\t", "").Replace("\r", "").Replace("\n", "") != "[]" &&
                rubric.PerformanceLevelsJson.Replace(" ", "").Replace("\t", "").Replace("\r", "").Replace("\n", "") != ""
        }));
        var rows = RubricTemplateReadMatcher.Apply(keys, input);
        var skip = ((long)input.Page - 1) * input.PageSize;
        return new RubricTemplatePageDto
        {
            Page = input.Page, PageSize = input.PageSize, TotalCount = rows.Count,
            Items = skip >= rows.Count ? [] : rows.Skip((int)skip).Take(input.PageSize).ToList(),
            Summary = new()
            {
                Total = rows.Count, Published = rows.Count(row => row.Status == RubricStatus.Published),
                Draft = rows.Count(row => row.Status == RubricStatus.Draft), Archived = rows.Count(row => row.Status == RubricStatus.Archived),
                Linked = rows.Sum(row => row.UsageCount)
            }
        };
    }
}
