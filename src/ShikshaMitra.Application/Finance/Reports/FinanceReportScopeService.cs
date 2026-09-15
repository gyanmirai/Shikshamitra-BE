using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Reports;

public class FinanceReportScopeService(
    FinanceReportRequestGuard requestGuard,
    IUserScopeAccessService userScopeAccessService,
    IRepository<AcademicYear, Guid> academicYearRepository,
    IRepository<EducationBoard, Guid> boardRepository,
    IRepository<EducationMedium, Guid> mediumRepository,
    IRepository<EducationMediumStage, Guid> stageRepository,
    IRepository<Standard, Guid> standardRepository,
    IRepository<Division, Guid> divisionRepository,
    ICurrentTenant currentTenant)
    : ITransientDependency
{
    public async Task<FinanceReportExecutionScope> AuthorizeAndNormalizeAsync(
        FinanceReportRequestDto request)
    {
        requestGuard.ValidateAndNormalize(request);

        var userScope = await userScopeAccessService.GetCurrentUserScopeAsync();
        userScopeAccessService.EnsureHasAnyAccess(userScope, FinanceReportRequestGuard.AccessDenied);

        var hierarchy = await GetHierarchyAsync();
        var allowedRows = hierarchy
            .Where(row => HasAccess(row, userScope))
            .ToList();

        EnsureSelectionsAreAccessible(request.Filters, allowedRows);

        var academicYears = await academicYearRepository.GetListAsync();
        var existingAcademicYearIds = academicYears.Select(year => year.Id).ToHashSet();
        EnsureIdentifiersExist(request.Filters.AcademicYearIds, existingAcademicYearIds, "AcademicYearIds");
        if (request.Filters.BatchId.HasValue && !existingAcademicYearIds.Contains(request.Filters.BatchId.Value))
        {
            throw AccessDenied("BatchId", request.Filters.BatchId.Value);
        }

        if (FinanceReportRequestGuard.RequiresAcademicYearWideScope(request.ReportType))
        {
            NormalizeAggregateAcademicYears(request, academicYears, userScope);
        }
        else
        {
            EnsureRequestedYearsAreAccessible(request.Filters, allowedRows, userScope);
        }

        return new FinanceReportExecutionScope
        {
            TenantId = currentTenant.Id,
            UserScope = userScope,
            AllowedStandardIds = allowedRows
                .Select(row => row.StandardId)
                .Distinct()
                .ToList()
        };
    }

    public async Task<IReadOnlyCollection<Guid>?> GetAccessibleAcademicYearIdsAsync()
    {
        var scope = await userScopeAccessService.GetCurrentUserScopeAsync();
        if (scope.HasTenantWideScope())
        {
            return null;
        }

        var hierarchy = await GetHierarchyAsync();
        return hierarchy
            .Where(row => HasAccess(row, scope))
            .Select(row => row.AcademicYearId)
            .Concat(scope.AcademicYearIds)
            .Concat(scope.WildcardBoardAcademicYearIds)
            .Distinct()
            .ToList();
    }

    public async Task<IReadOnlyCollection<Guid>> GetAggregateAcademicYearIdsAsync()
    {
        var scope = await userScopeAccessService.GetCurrentUserScopeAsync();
        var academicYears = await academicYearRepository.GetListAsync();
        return academicYears
            .Where(year => scope.HasAcademicYearWideScope(year.Id))
            .Select(year => year.Id)
            .ToList();
    }

    public async Task<IReadOnlyList<FinanceReportHierarchyOption>> GetAccessibleHierarchyAsync()
    {
        var scope = await userScopeAccessService.GetCurrentUserScopeAsync();
        return (await GetHierarchyAsync())
            .Where(row => HasAccess(row, scope))
            .ToList();
    }

    private async Task<List<FinanceReportHierarchyOption>> GetHierarchyAsync()
    {
        var academicYears = await academicYearRepository.GetListAsync();
        var boards = await boardRepository.GetListAsync();
        var mediums = await mediumRepository.GetListAsync();
        var stages = await stageRepository.GetListAsync();
        var standards = await standardRepository.GetListAsync();
        var divisions = await divisionRepository.GetListAsync();

        var academicYearById = academicYears.ToDictionary(year => year.Id);
        var boardById = boards.ToDictionary(board => board.Id);
        var mediumById = mediums.ToDictionary(medium => medium.Id);
        var stageById = stages.ToDictionary(stage => stage.Id);
        var divisionsByStandard = divisions
            .GroupBy(division => division.StandardId)
            .ToDictionary(group => group.Key, group => group
                .Select(division => new FinanceReportNamedOption(division.Id, division.Name))
                .ToList());

        var rows = new List<FinanceReportHierarchyOption>();
        foreach (var standard in standards)
        {
            if (!stageById.TryGetValue(standard.EducationMediumStageId, out var stage) ||
                !mediumById.TryGetValue(stage.EducationMediumId, out var medium) ||
                !boardById.TryGetValue(medium.EducationBoardId, out var board))
            {
                continue;
            }

            rows.Add(new FinanceReportHierarchyOption(
                board.AcademicYearId,
                academicYearById.GetValueOrDefault(board.AcademicYearId)?.Name ?? "Academic year",
                board.Id,
                board.Type.ToString(),
                medium.Id,
                medium.Name,
                stage.Id,
                stage.Name,
                standard.Id,
                standard.Name,
                divisionsByStandard.GetValueOrDefault(standard.Id) ?? []));
        }

        return rows;
    }

    private static bool HasAccess(FinanceReportHierarchyOption row, UserScopeContext scope) =>
        scope.HasTenantWideScope() ||
        scope.HasAcademicYearWideScope(row.AcademicYearId) ||
        scope.WildcardMediumBoardIds.Contains(row.BoardId) ||
        scope.WildcardMediumStageMediumIds.Contains(row.MediumId) ||
        scope.WildcardStandardMediumStageIds.Contains(row.StageId) ||
        scope.WildcardDivisionStandardIds.Contains(row.StandardId) ||
        row.DivisionIds.Any(scope.DivisionIds.Contains);

    private static void EnsureSelectionsAreAccessible(
        FinanceReportFilterDto filters,
        IReadOnlyCollection<FinanceReportHierarchyOption> allowedRows)
    {
        EnsureSelection(filters.BoardId, allowedRows.Select(row => row.BoardId), "BoardId");
        EnsureSelection(filters.MediumId, allowedRows.Select(row => row.MediumId), "MediumId");
        EnsureSelections(filters.OrganizationIds, allowedRows.Select(row => row.StageId), "OrganizationIds");
        EnsureSelections(filters.ClassIds, allowedRows.Select(row => row.StandardId), "ClassIds");
        EnsureSelections(filters.DivisionIds, allowedRows.SelectMany(row => row.DivisionIds), "DivisionIds");
    }

    private static void EnsureRequestedYearsAreAccessible(
        FinanceReportFilterDto filters,
        IReadOnlyCollection<FinanceReportHierarchyOption> allowedRows,
        UserScopeContext scope)
    {
        var requestedIds = (filters.AcademicYearIds ?? [])
            .Concat(filters.BatchId.HasValue ? [filters.BatchId.Value] : [])
            .Distinct()
            .ToList();
        var allowedIds = allowedRows.Select(row => row.AcademicYearId).ToHashSet();

        foreach (var yearId in requestedIds)
        {
            if (!allowedIds.Contains(yearId) &&
                !scope.AcademicYearIds.Contains(yearId) &&
                !scope.HasAcademicYearWideScope(yearId))
            {
                throw AccessDenied("AcademicYearId", yearId);
            }
        }
    }

    private static void NormalizeAggregateAcademicYears(
        FinanceReportRequestDto request,
        IReadOnlyCollection<AcademicYear> academicYears,
        UserScopeContext scope)
    {
        var allowedIds = academicYears
            .Where(year => scope.HasAcademicYearWideScope(year.Id))
            .Select(year => year.Id)
            .ToHashSet();
        var requestedIds = request.Filters.AcademicYearIds?.ToList() ?? [];

        if (requestedIds.Count == 0)
        {
            requestedIds = allowedIds.ToList();
        }

        var deniedId = requestedIds.FirstOrDefault(id => !allowedIds.Contains(id));
        if (deniedId != Guid.Empty)
        {
            throw AccessDenied("AcademicYearId", deniedId);
        }

        if (requestedIds.Count == 0)
        {
            throw new BusinessException(FinanceReportRequestGuard.AccessDenied)
                .WithData("Reason", "This aggregate needs full access to at least one academic year.");
        }

        request.Filters.AcademicYearIds = requestedIds.Distinct().ToList();
    }

    private static void EnsureSelection(Guid? requestedId, IEnumerable<Guid> allowedIds, string field)
    {
        if (requestedId.HasValue && !allowedIds.Contains(requestedId.Value))
        {
            throw AccessDenied(field, requestedId.Value);
        }
    }

    private static void EnsureSelections(
        IEnumerable<Guid>? requestedIds,
        IEnumerable<Guid> allowedIds,
        string field)
    {
        if (requestedIds == null)
        {
            return;
        }

        var allowed = allowedIds.ToHashSet();
        var deniedId = requestedIds.FirstOrDefault(id => !allowed.Contains(id));
        if (deniedId != Guid.Empty)
        {
            throw AccessDenied(field, deniedId);
        }
    }

    private static void EnsureIdentifiersExist(
        IEnumerable<Guid>? requestedIds,
        IReadOnlySet<Guid> existingIds,
        string field)
    {
        if (requestedIds == null)
        {
            return;
        }

        var missingId = requestedIds.FirstOrDefault(id => !existingIds.Contains(id));
        if (missingId != Guid.Empty)
        {
            throw AccessDenied(field, missingId);
        }
    }

    private static BusinessException AccessDenied(string field, Guid id) =>
        new BusinessException(FinanceReportRequestGuard.AccessDenied)
            .WithData("Field", field)
            .WithData("Id", id);

}

public sealed record FinanceReportHierarchyOption(
    Guid AcademicYearId,
    string AcademicYearName,
    Guid BoardId,
    string BoardName,
    Guid MediumId,
    string MediumName,
    Guid StageId,
    string StageName,
    Guid StandardId,
    string StandardName,
    IReadOnlyCollection<FinanceReportNamedOption> Divisions)
{
    public IReadOnlyCollection<Guid> DivisionIds => Divisions.Select(division => division.Id).ToList();
}

public sealed record FinanceReportNamedOption(Guid Id, string Name);
