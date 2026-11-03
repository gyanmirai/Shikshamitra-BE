using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.TenantSettings.Standards;
using ShikshaMitra.TenantSettings.Divisions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Finance.Reports.Builders;

[Volo.Abp.DependencyInjection.ExposeServices(typeof(IFinanceReportBuilder))]
public class FeeRegisterAllStudentBuilder
    : IFinanceReportBuilder, ITransientDependency
{
	private readonly FinanceReportPayerQueryService payerService;
	private readonly IRepository<FeeRecordMetadata, Guid> feeRecordRepository;
	private readonly IRepository<FeeCollectionDueFeeAllocation, Guid> allocationRepository;
	private readonly IRepository<DueFee, Guid> dueFeeRepository;
	private readonly IRepository<FeeHead, Guid> feeHeadRepository;
	private readonly IRepository<Standard, Guid> standardRepository;
	private readonly IRepository<Division, Guid> divisionRepository;
	private readonly IAsyncQueryableExecuter asyncExecuter;

	public FeeRegisterAllStudentBuilder(
		FinanceReportPayerQueryService payerService,
		IRepository<FeeRecordMetadata, Guid> feeRecordRepository,
		IRepository<PaymentMethodRecord, Guid> paymentMethodRepository,
		IRepository<FeeCollectionDueFeeAllocation, Guid> allocationRepository,
		IRepository<DueFee, Guid> dueFeeRepository,
		IRepository<FeeHead, Guid> feeHeadRepository,
		IRepository<Standard, Guid> standardRepository,
		IRepository<Division, Guid> divisionRepository,
		IAsyncQueryableExecuter asyncExecuter
	)
	{
		this.payerService = payerService;
		this.feeRecordRepository = feeRecordRepository;
		_ = paymentMethodRepository;
		this.allocationRepository = allocationRepository;
		this.dueFeeRepository = dueFeeRepository;
		this.feeHeadRepository = feeHeadRepository;
		this.standardRepository = standardRepository;
		this.divisionRepository = divisionRepository;
		this.asyncExecuter = asyncExecuter;
	}

    public string ReportType => FinanceReportTypes.FeeRegisterAllStudent;

    public async Task<FinanceReportPreviewDto> BuildAsync(FinanceReportRequestDto request, FinanceReportPreviewDto payload, FinanceReportExecutionScope executionScope, bool exportAllRows)
    {

        // Use canonical payer identities; applicant account evidence keeps its original scope.
        var payers = await payerService.GetPayersAsync(request.Filters, executionScope);

        if (payers.Count == 0) return payload;
        // 2. Fetch Payments for these payers (no date filter)
        var metadataQuery = FinanceReportPayerQueryService.FilterCollections(await feeRecordRepository.GetQueryableAsync(), payers);

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

        payload.Columns.Add(new() { Id = "RollNo", Label = "Roll No." });
        payload.Columns.Add(new() { Id = "GRNo", Label = "Id No." });
        payload.Columns.Add(new() { Id = "StudentName", Label = "Name" });
        payload.Columns.Add(new() { Id = "Class", Label = "Class" });
        payload.Columns.Add(new() { Id = "Division", Label = "Division" });

        foreach (var feeHead in targetFeeHeads)
        {
            payload.Columns.Add(new() { Id = $"FH_{feeHead.Id}", Label = feeHead.Name });
        }
        payload.Columns.Add(new() { Id = "Total", Label = "Total" });

        // 5. Fetch Standard and Division names
        var stdIds = payers.Where(s => s.StandardId.HasValue).Select(s => s.StandardId!.Value).Distinct().ToList();
        var divIds = payers.Where(s => s.DivisionId.HasValue).Select(s => s.DivisionId!.Value).Distinct().ToList();

        var standards = await asyncExecuter.ToListAsync((await standardRepository.GetQueryableAsync()).Where(s => stdIds.Contains(s.Id)));
        var divisions = await asyncExecuter.ToListAsync((await divisionRepository.GetQueryableAsync()).Where(d => divIds.Contains(d.Id)));

        var stdMap = standards.ToDictionary(s => s.Id, s => s.Name);
        var divMap = divisions.ToDictionary(d => d.Id, d => d.Name);

        // Group allocations by canonical payer and fee head.
        var groupedData = new Dictionary<FinanceReportPayerKey, Dictionary<Guid, double>>();
        var metadataMap = metadataList.ToDictionary(m => m.Id, m => FinanceReportPayerKey.From(m.StudentId, m.FinanceFeeAccountId));

        foreach (var allocation in allocationsList)
        {
            if (!metadataMap.TryGetValue(allocation.FeeRecordMetadataId, out var payerKey)) continue;
            if (!dueFeeMap.TryGetValue(allocation.DueFeeId, out var feeHeadId)) continue;

            if (!groupedData.ContainsKey(payerKey)) groupedData[payerKey] = new();
            if (!groupedData[payerKey].ContainsKey(feeHeadId)) groupedData[payerKey][feeHeadId] = 0;

            groupedData[payerKey][feeHeadId] += allocation.Amount;
        }

        // 7. Build Rows
        var rows = new List<Dictionary<string, string?>>();
        foreach (var payer in payers)
        {
            // Only include payers that have payments in this range
            if (!groupedData.TryGetValue(payer.Key, out var feeHeadData))
            {
                continue;
            }

            var payerName = payer.DisplayName;
            var stdName = payer.StandardId.HasValue && stdMap.TryGetValue(payer.StandardId.Value, out var sn) ? sn : "N/A";
            var divName = payer.DivisionId.HasValue && divMap.TryGetValue(payer.DivisionId.Value, out var dn) ? dn : "N/A";

            var row = new Dictionary<string, string?>
            {
                { "RollNo", payer.RollNo?.ToString() ?? "N/A" },
                { "GRNo", payer.GrNo?.ToString() ?? "N/A" },
                { "StudentName", payerName },
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
