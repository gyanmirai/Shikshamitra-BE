using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.Standards;
using ShikshaMitra.TenantSettings.Divisions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Finance.Reports.Builders;

[Volo.Abp.DependencyInjection.ExposeServices(typeof(IFinanceReportBuilder))]
public class FeeCollectionStudentWithFeewiseBuilder(
    IRepository<Student, Guid> studentRepository,
    IRepository<FeeRecordMetadata, Guid> feeRecordRepository,
    IRepository<PaymentMethodRecord, Guid> paymentMethodRepository,
    IRepository<FeeCollectionDueFeeAllocation, Guid> allocationRepository,
    IRepository<DueFee, Guid> dueFeeRepository,
    IRepository<FeeHead, Guid> feeHeadRepository,
    IRepository<Standard, Guid> standardRepository,
    IRepository<Division, Guid> divisionRepository,
    IAsyncQueryableExecuter asyncExecuter)
    : IFinanceReportBuilder, ITransientDependency
{
    public string ReportType => FinanceReportTypes.FeeCollectionStudentWithFeewise;

    public async Task<FinanceReportPreviewDto> BuildAsync(FinanceReportRequestDto request, FinanceReportPreviewDto payload, FinanceReportExecutionScope executionScope, bool exportAllRows)
    {
        var boardId = request.Filters.BoardId;
        var mediumId = request.Filters.MediumId;
        var organizationIds = request.Filters.OrganizationIds ?? new List<Guid>();
        var classIds = request.Filters.ClassIds ?? new List<Guid>();
        var divisionIds = request.Filters.DivisionIds ?? new List<Guid>();

        var fromDate = request.Filters.FromDate;
        var toDate = request.Filters.ToDate;

        // 1. Fetch Students
        var studentQuery = Students.StudentScopeAccessService.ApplyScope(
                await studentRepository.GetQueryableAsync(), executionScope.UserScope)
            .WhereIf(request.Filters.AcademicYearIds?.Any() == true,
                s => s.AcademicYearId.HasValue && request.Filters.AcademicYearIds!.Contains(s.AcademicYearId.Value))
            .WhereIf(request.Filters.BatchId.HasValue, s => s.AcademicYearId == request.Filters.BatchId)
            .WhereIf(boardId.HasValue, s => s.AcademicBoardId == boardId)
            .WhereIf(mediumId.HasValue, s => s.AcademicMediumId == mediumId)
            .WhereIf(organizationIds.Any(), s => s.AcademicMediumStageId.HasValue && organizationIds.Contains(s.AcademicMediumStageId.Value))
            .WhereIf(classIds.Any(), s => s.StandardId.HasValue && classIds.Contains(s.StandardId.Value))
            .WhereIf(divisionIds.Any(), s => s.DivisionId.HasValue && divisionIds.Contains(s.DivisionId.Value));

        var students = await asyncExecuter.ToListAsync(studentQuery);
        var studentIds = students.Select(s => s.Id).ToList();

        if (!studentIds.Any())
        {
            return payload; // Return empty if no students matched
        }

        // 2. Fetch Payments for these students in date range
        var metadataQuery = (await feeRecordRepository.GetQueryableAsync())
            .Where(m => studentIds.Contains(m.StudentId))
            .WhereIf(fromDate.HasValue, m => m.CreationTime >= fromDate)
            .WhereIf(toDate.HasValue, m => m.CreationTime <= toDate);

        var metadataList = await asyncExecuter.ToListAsync(metadataQuery);
        var metadataIds = metadataList.Select(m => m.Id).ToList();

        if (!metadataIds.Any())
        {
            return payload;
        }

        // 3. Get Allocations & DueFees to find the FeeHeads
        var allocationsQuery = (await allocationRepository.GetQueryableAsync())
            .Where(a => metadataIds.Contains(a.FeeRecordMetadataId));
        var allocationsList = await asyncExecuter.ToListAsync(allocationsQuery);

        var dueFeeIds = allocationsList.Select(a => a.DueFeeId).Distinct().ToList();
        var dueFeesQuery = (await dueFeeRepository.GetQueryableAsync())
            .Where(d => dueFeeIds.Contains(d.Id));
        var dueFeesList = await asyncExecuter.ToListAsync(dueFeesQuery);
        var dueFeeMap = dueFeesList.ToDictionary(d => d.Id, d => d.FeeHeadId);

        var usedFeeHeadIds = dueFeeMap.Values.Distinct().ToList();

        // 4. Fetch FeeHeads to build dynamic columns
        var feeHeadsQuery = (await feeHeadRepository.GetQueryableAsync())
            .Where(f => usedFeeHeadIds.Contains(f.Id));
        var targetFeeHeads = await asyncExecuter.ToListAsync(feeHeadsQuery);

        payload.Columns.Add(new() { Id = "StudentName", Label = "Student Name" });
        payload.Columns.Add(new() { Id = "GRNo", Label = "GR No" });
        payload.Columns.Add(new() { Id = "Class", Label = "Class" });
        payload.Columns.Add(new() { Id = "Division", Label = "Division" });

        foreach (var feeHead in targetFeeHeads)
        {
            payload.Columns.Add(new() { Id = $"FH_{feeHead.Id}", Label = feeHead.Name });
        }
        payload.Columns.Add(new() { Id = "Total", Label = "Total" });

        // 5. Fetch Standard and Division names
        var stdIds = students.Where(s => s.StandardId.HasValue).Select(s => s.StandardId!.Value).Distinct().ToList();
        var divIds = students.Where(s => s.DivisionId.HasValue).Select(s => s.DivisionId!.Value).Distinct().ToList();

        var standards = await asyncExecuter.ToListAsync((await standardRepository.GetQueryableAsync()).Where(s => stdIds.Contains(s.Id)));
        var divisions = await asyncExecuter.ToListAsync((await divisionRepository.GetQueryableAsync()).Where(d => divIds.Contains(d.Id)));

        var stdMap = standards.ToDictionary(s => s.Id, s => s.Name);
        var divMap = divisions.ToDictionary(d => d.Id, d => d.Name);

        // 6. Group Data: StudentId -> FeeHeadId -> Amount
        var groupedData = new Dictionary<Guid, Dictionary<Guid, double>>();
        var metadataMap = metadataList.ToDictionary(m => m.Id, m => m.StudentId);

        foreach (var allocation in allocationsList)
        {
            if (!metadataMap.TryGetValue(allocation.FeeRecordMetadataId, out var studentId)) continue;
            if (!dueFeeMap.TryGetValue(allocation.DueFeeId, out var feeHeadId)) continue;

            if (!groupedData.ContainsKey(studentId)) groupedData[studentId] = new();
            if (!groupedData[studentId].ContainsKey(feeHeadId)) groupedData[studentId][feeHeadId] = 0;

            groupedData[studentId][feeHeadId] += allocation.Amount;
        }

        // 7. Build Rows
        var rows = new List<Dictionary<string, string?>>();
        foreach (var student in students)
        {
            // Only include students that have payments in this range
            if (!groupedData.TryGetValue(student.Id, out var feeHeadData))
            {
                continue;
            }

            var studentName = $"{student.FirstName} {student.MiddleName} {student.LastName}".Replace("  ", " ").Trim();
            var stdName = student.StandardId.HasValue && stdMap.TryGetValue(student.StandardId.Value, out var sn) ? sn : "N/A";
            var divName = student.DivisionId.HasValue && divMap.TryGetValue(student.DivisionId.Value, out var dn) ? dn : "N/A";

            var row = new Dictionary<string, string?>
            {
                { "StudentName", studentName },
                { "GRNo", student.GrNo?.ToString() ?? "N/A" },
                { "Class", stdName },
                { "Division", divName }
            };

            double rowTotal = 0;
            foreach (var feeHead in targetFeeHeads)
            {
                if (feeHeadData.TryGetValue(feeHead.Id, out var amount))
                {
                    row[$"FH_{feeHead.Id}"] = Math.Ceiling(amount).ToString("N0");
                    rowTotal += amount;
                }
                else
                {
                    row[$"FH_{feeHead.Id}"] = "0.00";
                }
            }

            row["Total"] = Math.Ceiling(rowTotal).ToString("N0");
            rows.Add(row);
        }

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;

        var sortedRows = rows.OrderBy(r => r["Class"]).ThenBy(r => r["Division"]).ThenBy(r => r["StudentName"]).ToList();

        payload.Rows = exportAllRows
            ? sortedRows
            : sortedRows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();

        return payload;
    }
}
