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

        var scopedStudentIds = await financeScopeAccessService.GetScopedStudentIdsQueryAsync();

        var filter = new ReconciliationListFilter
        {
            AcademicYearId        = input.AcademicYearId,
            Month                 = input.Month,
            Year                  = input.Year,
            PaymentMethod         = input.PaymentMethod,
            VerificationStatus    = input.VerificationStatus,
            Search                = input.Search,
            Page                  = Math.Max(1, input.Page),
            PageSize              = Math.Clamp(input.PageSize, 1, 100),
            ScopedStudentIdsQuery = scopedStudentIds,
            ScopedAccountIdsQuery = await financeScopeAccessService.GetScopedFeeAccountIdsQueryAsync(),
        };

        var (totalCount, ids) = await receiptRepository
            .GetReconciliationPagedIdsAsync(filter, cancellationToken);
        var rows = ids.Count > 0
            ? await receiptRepository.GetReconciliationRowsByIdsAsync(ids, cancellationToken)
            : [];

        var tabCounts = await GetTabCountsCoreAsync(
            input.AcademicYearId, input.Month, input.Year, scopedStudentIds, cancellationToken);

        var pageSize = filter.PageSize;
        return new ReconciliationListResponse
        {
            Success    = true,
            Items      = rows.Select(MapRow).ToList(),
            TotalCount = totalCount,
            CurrentPage = filter.Page,
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
