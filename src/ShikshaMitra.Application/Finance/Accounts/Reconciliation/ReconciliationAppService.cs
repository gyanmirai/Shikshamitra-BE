using System;
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
		var hasHeaderFilters = tableQuery.Filters.Count > 0;
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
            // The repository applies the same scope/search/order without an internal page-size cap.
            Page                  = hasHeaderFilters ? 1 : page,
            PageSize              = hasHeaderFilters ? int.MaxValue : pageSize,
            ScopedStudentIdsQuery = scopedStudentIds,
            ScopedAccountIdsQuery = await financeScopeAccessService.GetScopedFeeAccountIdsQueryAsync(),
        };

        var (totalCount, ids) = await receiptRepository
            .GetReconciliationPagedIdsAsync(filter, cancellationToken);
        var rows = ids.Count > 0
            ? await receiptRepository.GetReconciliationRowsByIdsAsync(ids, cancellationToken)
            : [];
		var items = rows.Select(MapRow).ToList();
		if (hasHeaderFilters)
		{
			var matching = tableQuery.Apply(items.AsQueryable(), ListFilterFields).ToList();
			totalCount = matching.Count;
			var skip = (int)Math.Min((long)(page - 1) * pageSize, int.MaxValue);
			items = matching.Skip(skip).Take(pageSize).ToList();
		}

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

    public async Task<ReconciliationTabCountsDto> GetTabCountsAsync(
        Guid academicYearId, int? month, int? year,
        CancellationToken cancellationToken = default)
    {
        var scopedStudentIds = await financeScopeAccessService.GetScopedStudentIdsQueryAsync();
        return await GetTabCountsCoreAsync(academicYearId, month, year, scopedStudentIds, cancellationToken);
    }
}
