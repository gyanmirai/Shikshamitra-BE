using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Accounts.Reconciliation.Dtos;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.Receipts;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Users;

namespace ShikshaMitra.Finance.Accounts.Reconciliation;

[RemoteService(false)]
[Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Reconciliation.View)]
public partial class ReconciliationAppService(
    IFeePaymentReceiptRepository receiptRepository,
    ReconciliationManager reconciliationManager,
    FinanceScopeAccessService financeScopeAccessService,
    ICurrentUser currentUser,
    IDistributedEventBus eventBus,
    ILogger<ReconciliationAppService> logger
) : ApplicationService, IReconciliationAppService
{
    public async Task<ReconciliationListResponse> GetListAsync(
        GetReconciliationListInput input,
        CancellationToken cancellationToken = default)
    {
        if (input.AcademicYearId == Guid.Empty)
            throw new BusinessException("AcademicYearId is required.");
		var tableQuery = ParseListFilters(input.Filters, input.PaymentMethod);
        var hasRenderedFilters = tableQuery.Filters.Any(f => !ReconciliationHeaderColumns.IsScalar(f.Column, input.PaymentMethod));
		var page = Math.Max(1, input.Page);
		var pageSize = Math.Clamp(input.PageSize, 1, 100);

        var scopedStudentIds = await financeScopeAccessService.GetScopedStudentIdsQueryAsync();

        var filter = new ReconciliationListFilter
        {
            AcademicYearId        = input.AcademicYearId,
            Month                 = input.Month,
            Year                  = input.Year,
            PaymentMethod         = input.PaymentMethod,
            VerificationStatus    = input.VerificationStatus,
            Search                = input.Search,
            TableQuery            = tableQuery,
            Page                  = page,
            PageSize              = pageSize,
            ScopedStudentIdsQuery = scopedStudentIds,
            ScopedAccountIdsQuery = await financeScopeAccessService.GetScopedFeeAccountIdsQueryAsync(),
        };

        var (totalCount, ids) = hasRenderedFilters
            ? await GetRenderedFilterPageAsync(filter, tableQuery, page, pageSize, cancellationToken)
            : await receiptRepository.GetReconciliationPagedIdsAsync(filter, cancellationToken);
        var rows = ids.Count > 0
            ? await receiptRepository.GetReconciliationRowsByIdsAsync(ids, cancellationToken)
            : [];
		var items = rows.Select(MapRow).ToList();

        var tabCounts = await GetTabCountsCoreAsync(
            input.AcademicYearId, input.Month, input.Year, scopedStudentIds, cancellationToken);

        return new ReconciliationListResponse
        {
            Success    = true,
            Items      = items,
            TotalCount = totalCount,
            CurrentPage = page,
            PageSize   = pageSize,
            TotalPages = (int)Math.Ceiling((double)totalCount / pageSize),
            TabCounts  = tabCounts,
        };
    }

    private async Task<(int TotalCount, List<Guid> Ids)> GetRenderedFilterPageAsync(
        ReconciliationListFilter filter, ShikshaMitra.Finance.Common.FinanceTableQuery table,
        int page, int pageSize, CancellationToken cancellationToken)
    {
        // Legacy instrument values use the existing .NET date/display rules. Scan
        // narrow bounded candidates; only the selected IDs receive detail enrichment.
        const int batchSize = 256;
        var skip = (long)(page - 1) * pageSize;
        var total = 0;
        var selected = new List<Guid>(pageSize);
        for (var offset = 0; ; offset = checked(offset + batchSize))
        {
            var candidates = await receiptRepository.GetReconciliationFilterCandidatesAsync(filter, offset, batchSize, cancellationToken);
            var matches = table.Apply(candidates.Select(MapRow).AsQueryable(), ListFilterFields);
            foreach (var row in matches)
            {
                if (total >= skip && selected.Count < pageSize) selected.Add(row.Id);
                total++;
            }
            if (candidates.Count < batchSize) break;
        }
        return (total, selected);
    }

    public async Task<ReconciliationTabCountsDto> GetTabCountsAsync(
        Guid academicYearId, int? month, int? year,
        CancellationToken cancellationToken = default)
    {
        var scopedStudentIds = await financeScopeAccessService.GetScopedStudentIdsQueryAsync();
        return await GetTabCountsCoreAsync(academicYearId, month, year, scopedStudentIds, cancellationToken);
    }
}
