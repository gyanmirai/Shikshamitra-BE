using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Finance.Reports.Builders;

[Volo.Abp.DependencyInjection.ExposeServices(typeof(IFinanceReportBuilder))]
public class ClasswiseFeeSetupBuilder(
    IRepository<FeeStructureMetadata, Guid> metadataRepository,
    IRepository<FeeStructure, Guid> feeStructureRepository,
    IRepository<FeeStructureDetail, Guid> feeStructureDetailRepository,
    IRepository<FeeHead, Guid> feeHeadRepository,
    IRepository<Standard, Guid> standardRepository,
    IAsyncQueryableExecuter asyncExecuter)
    : IFinanceReportBuilder, ITransientDependency
{
    public string ReportType => FinanceReportTypes.ClasswiseFeeSetup;

    public async Task<FinanceReportPreviewDto> BuildAsync(FinanceReportRequestDto request, FinanceReportPreviewDto payload, FinanceReportExecutionScope executionScope, bool exportAllRows)
    {
        var boardId = request.Filters.BoardId;
        var mediumId = request.Filters.MediumId;
        var organizationIds = request.Filters.OrganizationIds ?? new List<Guid>();
        var batchId = request.Filters.BatchId; // AcademicYearId

        // 1. Fetch relevant Standards based on Organization (Stage)
        var allowedStandardIds = executionScope.AllowedStandardIds;
        var standardQuery = (await standardRepository.GetQueryableAsync())
            .Where(s => allowedStandardIds.Contains(s.Id))
            .WhereIf(organizationIds.Any(), s => organizationIds.Contains(s.EducationMediumStageId));
        var standards = await asyncExecuter.ToListAsync(standardQuery);
        var standardIds = standards.Select(s => s.Id).ToList();

        if (!standardIds.Any()) return payload;

        // 2. Fetch FeeStructureMetadata
        var metaQuery = (await metadataRepository.GetQueryableAsync())
            .WhereIf(boardId.HasValue, m => m.AcademicBoardId == boardId)
            .WhereIf(mediumId.HasValue, m => m.AcademicMediumId == mediumId)
            .WhereIf(batchId.HasValue, m => m.AcademicYearId == batchId);
        var metadatas = await asyncExecuter.ToListAsync(metaQuery);
        var metaIds = metadatas.Select(m => m.Id).ToList();

        if (!metaIds.Any()) return payload;

        // 3. Fetch FeeStructures
        var structQuery = (await feeStructureRepository.GetQueryableAsync())
            .Where(fs => metaIds.Contains(fs.FeeStructureMetadataId) && standardIds.Contains(fs.StandardId));
        var structures = await asyncExecuter.ToListAsync(structQuery);
        var structureIds = structures.Select(s => s.Id).ToList();

        if (!structureIds.Any()) return payload;

        // 4. Fetch Details
        var detailsQuery = (await feeStructureDetailRepository.GetQueryableAsync())
            .Where(d => structureIds.Contains(d.FeeStructureId));
        var details = await asyncExecuter.ToListAsync(detailsQuery);

        // 5. Fetch FeeHeads
        var feeHeadIds = details.Select(d => d.FeeHeadId).Distinct().ToList();
        var feeHeads = await asyncExecuter.ToListAsync((await feeHeadRepository.GetQueryableAsync()).Where(f => feeHeadIds.Contains(f.Id)));
        var feeHeadMap = feeHeads.ToDictionary(f => f.Id, f => f.Name);

        // 6. Build Grid (Rows = FeeHeads, Columns = Standards)
        var usedStandards = standards.Where(s => structures.Select(x => x.StandardId).Contains(s.Id)).OrderBy(s => s.Name).ToList();

        payload.Columns.Add(new() { Id = "FeeName", Label = "Fee Name" });
        foreach (var std in usedStandards)
        {
            payload.Columns.Add(new() { Id = $"STD_{std.Id}", Label = std.Name });
        }

        var rows = new List<Dictionary<string, string?>>();
        var groupedByFeeHead = details.GroupBy(d => d.FeeHeadId).ToList();

        // Sort Fee Heads alphabetically
        var sortedFeeHeads = groupedByFeeHead
            .Select(g => new { FeeHeadId = g.Key, Name = feeHeadMap.TryGetValue(g.Key, out var n) ? n : "Unknown", Details = g.ToList() })
            .OrderBy(x => x.Name)
            .ToList();

        foreach (var feeHeadData in sortedFeeHeads)
        {
            var row = new Dictionary<string, string?>
            {
                { "FeeName", feeHeadData.Name }
            };

            foreach (var std in usedStandards)
            {
                // Find structure for this standard
                var structure = structures.FirstOrDefault(s => s.StandardId == std.Id);
                if (structure != null)
                {
                    // Find detail
                    var detail = feeHeadData.Details.FirstOrDefault(d => d.FeeStructureId == structure.Id);
                    row[$"STD_{std.Id}"] = detail != null ? detail.Amount.ToString("N2") : "-";
                }
                else
                {
                    row[$"STD_{std.Id}"] = "-";
                }
            }

            rows.Add(row);
        }

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;

        payload.Rows = exportAllRows
            ? rows
            : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();

        return payload;
    }
}
